import type { Context } from 'hono';
import { verifyToken } from '../tokens';
import type { AppBindings } from '../types';

/**
 * ユーザー情報エンドポイント。
 *
 * .NET 版と同じく `sub` のみを返す。EcAuth 側はこの値を
 * ExternalIdpMapping.ExternalSubject として保存し、JIT プロビジョニングの
 * キーに使う。
 */
export async function handleUserinfo(c: Context<AppBindings>) {
  const tenant = c.get('tenant');

  const authorization = c.req.header('Authorization');
  if (!authorization) return c.json({ error: 'invalid_request' });

  const spaceIndex = authorization.indexOf(' ');
  if (spaceIndex === -1) return c.json({ error: 'invalid_request' });

  // .NET 版は大文字小文字を区別して "Bearer" のみ受け付けていた挙動に合わせる。
  const scheme = authorization.slice(0, spaceIndex);
  if (scheme !== 'Bearer') return c.json({ error: 'invalid_request' });

  const token = authorization.slice(spaceIndex + 1).trim();
  if (!token) return c.json({ error: 'invalid_request' });

  const claims = await verifyToken(c.env, token, 'at');
  if (!claims) return c.json({ error: 'invalid_token' });
  if (claims.org !== tenant.org) return c.json({ error: 'invalid_token' });

  return c.json({ sub: claims.sub });
}
