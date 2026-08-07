import type { Context } from 'hono';
import { safeEqual } from '../compare';
import { findClient } from '../tenants';
import { CODE_TTL_SECONDS, issueToken } from '../tokens';
import type { AppBindings } from '../types';

const BASIC_REALM = 'Basic realm="Authorization Required"';

function unauthorized(c: Context<AppBindings>, message: string) {
  c.header('WWW-Authenticate', BASIC_REALM);
  return c.text(message, 401);
}

/** Base64 の Basic 認証パラメータを UTF-8 文字列に復号する。 */
function decodeBasicParameter(parameter: string): string | null {
  try {
    const bytes = Uint8Array.from(atob(parameter), (char) => char.charCodeAt(0));
    return new TextDecoder().decode(bytes);
  } catch {
    return null;
  }
}

/**
 * 認可エンドポイント。
 *
 * ログイン画面は持たず、HTTP Basic 認証が通ればそのまま redirect_uri へ
 * 認可コード付きでリダイレクトする（.NET 版と同じ挙動）。EcAuth の E2E は
 * Playwright の httpCredentials でこの動作に依存している。
 */
export async function handleAuthorization(c: Context<AppBindings>) {
  const tenant = c.get('tenant');

  const authorization = c.req.header('Authorization');
  if (!authorization) return unauthorized(c, 'Missing Authorization Header');

  const spaceIndex = authorization.indexOf(' ');
  const scheme = spaceIndex === -1 ? authorization : authorization.slice(0, spaceIndex);
  const parameter = spaceIndex === -1 ? '' : authorization.slice(spaceIndex + 1).trim();

  if (scheme.toLowerCase() !== 'basic') return unauthorized(c, 'Invalid Authorization Scheme');
  if (!parameter) return unauthorized(c, 'Missing Authorization Parameter');

  const decoded = decodeBasicParameter(parameter);
  if (decoded === null) return unauthorized(c, 'Invalid username or password');

  const colonIndex = decoded.indexOf(':');
  if (colonIndex === -1) return unauthorized(c, 'Invalid username or password');

  const email = decoded.slice(0, colonIndex);
  const password = decoded.slice(colonIndex + 1);

  const [emailMatches, passwordMatches] = await Promise.all([
    safeEqual(email, tenant.userEmail),
    safeEqual(password, tenant.userPassword),
  ]);
  if (!emailMatches || !passwordMatches) {
    return unauthorized(c, 'Invalid username or password');
  }

  const clientId = c.req.query('client_id');
  const redirectUri = c.req.query('redirect_uri');
  const state = c.req.query('state');
  const nonce = c.req.query('nonce');

  if (!clientId || !redirectUri) {
    return c.json(
      {
        error: 'invalid_request',
        error_description: 'client_id and redirect_uri are required',
      },
      400,
    );
  }

  // client_id が未知、または redirect_uri が登録値と一致しない要求は 400 で返す。
  //
  // .NET 版はこの場合も要求された redirect_uri へエラー付きでリダイレクトして
  // いたが、これは任意の URL へ遷移させられるオープンリダイレクトであり、
  // 公開エンドポイントでは悪用されうる。RFC 6749 §4.1.2.1 も
  // 「redirect_uri が不正なら リダイレクトしてはならない」と定めているため、
  // 仕様準拠かつ安全な側に倒す。
  const client = findClient(tenant, clientId);
  if (!client || redirectUri !== client.redirectUri) {
    return c.json(
      {
        error: 'invalid_request_uri',
        error_description:
          'The redirect_uri in the Authorization Request does not match the registered value.',
      },
      400,
    );
  }

  const code = await issueToken(c.env, 'code', CODE_TTL_SECONDS, {
    org: tenant.org,
    cid: client.clientId,
    sub: tenant.userSubject,
    ruri: redirectUri,
    ...(nonce !== undefined ? { nonce } : {}),
  });

  const location = new URL(redirectUri);
  location.searchParams.set('code', code);
  if (state !== undefined) location.searchParams.set('state', state);
  if (nonce !== undefined) location.searchParams.set('nonce', nonce);

  return c.redirect(location.toString(), 302);
}
