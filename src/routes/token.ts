import type { Context } from 'hono';
import { safeEqual } from '../compare';
import { findClient } from '../tenants';
import {
  ACCESS_TOKEN_TTL_SECONDS,
  CODE_TTL_SECONDS,
  REFRESH_TOKEN_TTL_SECONDS,
  issueToken,
  verifyToken,
} from '../tokens';
import type { AppBindings } from '../types';

/**
 * OAuth2 エラーを返す。
 *
 * RFC 6749 §5.2 は 400 を求めているが、.NET 版が HTTP 200 + `{"error": ...}` を
 * 返していたため、その挙動を維持する。EcAuth 側は access_token の有無で判定して
 * いる（AuthorizationCallbackController）ため、どちらでも動作する。
 */
function oauthError(c: Context<AppBindings>, error: string) {
  return c.json({ error });
}

/**
 * トークンエンドポイント。
 * authorization_code / refresh_token の 2 つの grant をサポートする。
 */
export async function handleToken(c: Context<AppBindings>) {
  const tenant = c.get('tenant');

  let form: FormData;
  try {
    form = await c.req.formData();
  } catch {
    return oauthError(c, 'invalid_request');
  }

  const field = (name: string): string | undefined => {
    const value = form.get(name);
    return typeof value === 'string' && value.length > 0 ? value : undefined;
  };

  const clientId = field('client_id');
  const clientSecret = field('client_secret');
  if (!clientId || !clientSecret) return oauthError(c, 'invalid_request');

  // client_id は識別子なので通常比較で引き当て、client_secret のみ定数時間で照合する。
  const client = findClient(tenant, clientId);
  if (!client) return oauthError(c, 'invalid_client');
  if (!(await safeEqual(clientSecret, client.clientSecret))) {
    return oauthError(c, 'invalid_client');
  }

  const grantType = field('grant_type');

  if (grantType === 'authorization_code') {
    const code = field('code');
    const redirectUri = field('redirect_uri');
    if (!code || !redirectUri) return oauthError(c, 'invalid_request');

    const claims = await verifyToken(c.env, code, 'code');
    if (!claims) return oauthError(c, 'invalid_grant');
    if (claims.org !== tenant.org || claims.cid !== client.clientId) {
      return oauthError(c, 'invalid_grant');
    }
    // 認可要求時の redirect_uri と一致することを確認する（RFC 6749 §4.1.3）。
    if (claims.ruri !== redirectUri) return oauthError(c, 'invalid_grant');

    // 使用済み判定。.NET 版の authorization_code.used 相当。
    //
    // これは「一度使ったコードを後から使い回す」逐次的な再利用を防ぐもので、
    // 厳密な単回使用の保証ではない。Cloudflare KV は結果整合であり、
    // 下の get → put は原子的な read-modify-write ではないため、同一コードを
    // 別エッジから同時に交換された場合は複数が成功しうる。
    // 厳密な排他が必要になったら Durable Objects へ移すこと。
    if (await c.env.USED_CODES.get(claims.jti)) return oauthError(c, 'invalid_grant');
    await c.env.USED_CODES.put(claims.jti, '1', { expirationTtl: CODE_TTL_SECONDS });

    const [accessToken, refreshToken] = await Promise.all([
      issueToken(c.env, 'at', ACCESS_TOKEN_TTL_SECONDS, {
        org: tenant.org,
        cid: client.clientId,
        sub: claims.sub,
      }),
      issueToken(c.env, 'rt', REFRESH_TOKEN_TTL_SECONDS, {
        org: tenant.org,
        cid: client.clientId,
        sub: claims.sub,
      }),
    ]);

    return c.json({
      access_token: accessToken,
      token_type: 'Bearer',
      expires_in: ACCESS_TOKEN_TTL_SECONDS,
      refresh_token: refreshToken,
    });
  }

  if (grantType === 'refresh_token') {
    const refreshToken = field('refresh_token');
    if (!refreshToken) return oauthError(c, 'invalid_request');

    const claims = await verifyToken(c.env, refreshToken, 'rt');
    if (!claims) return oauthError(c, 'invalid_grant');
    if (claims.org !== tenant.org || claims.cid !== client.clientId) {
      return oauthError(c, 'invalid_grant');
    }

    const accessToken = await issueToken(c.env, 'at', ACCESS_TOKEN_TTL_SECONDS, {
      org: tenant.org,
      cid: client.clientId,
      sub: claims.sub,
    });

    // .NET 版と同じく、リフレッシュトークンはローテーションせず同じ値を返す。
    return c.json({
      access_token: accessToken,
      token_type: 'Bearer',
      expires_in: ACCESS_TOKEN_TTL_SECONDS,
      refresh_token: refreshToken,
    });
  }

  return oauthError(c, 'unsupported_grant_type');
}
