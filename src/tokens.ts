import type { Env } from './env';

/**
 * トークン種別。
 * - `code`: 認可コード
 * - `at`: アクセストークン
 * - `rt`: リフレッシュトークン
 */
export type TokenType = 'code' | 'at' | 'rt';

/** 認可コードの有効期間（秒）。.NET 版と同じ 1 時間。 */
export const CODE_TTL_SECONDS = 3600;
/** アクセストークンの有効期間（秒）。 */
export const ACCESS_TOKEN_TTL_SECONDS = 3600;
/** リフレッシュトークンの有効期間（秒）。.NET 版と同じ 30 日。 */
export const REFRESH_TOKEN_TTL_SECONDS = 60 * 60 * 24 * 30;

export interface TokenClaims {
  typ: TokenType;
  /** 発行元テナント。テナントをまたいだトークン利用を防ぐ。 */
  org: string;
  /** client_id */
  cid: string;
  /** ユーザー識別子。`/userinfo` がそのまま `sub` として返す。 */
  sub: string;
  /** トークン固有 ID。認可コードの単回使用判定に使う。 */
  jti: string;
  iat: number;
  exp: number;
  /** 認可コードのみ: 発行時の redirect_uri。トークン要求時に一致を検証する。 */
  ruri?: string;
  /** 認可コードのみ: 認可要求で渡された nonce。 */
  nonce?: string;
}

const JWT_HEADER = { alg: 'HS256', typ: 'JWT' } as const;

export function nowSeconds(): number {
  return Math.floor(Date.now() / 1000);
}

function base64UrlEncode(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function base64UrlDecode(value: string): Uint8Array {
  const padding = value.length % 4 === 0 ? '' : '='.repeat(4 - (value.length % 4));
  const binary = atob(value.replace(/-/g, '+').replace(/_/g, '/') + padding);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

async function importSigningKey(secret: string): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    'raw',
    new TextEncoder().encode(secret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign', 'verify'],
  );
}

/**
 * クレームを HS256 署名付きの JWT にする。
 *
 * クライアントから見れば不透明な文字列だが、JWT 形式にしておくことで
 * 障害調査時に標準ツールでデコードできる。
 */
export async function signToken(env: Env, claims: TokenClaims): Promise<string> {
  const encoder = new TextEncoder();
  const key = await importSigningKey(env.TOKEN_SIGNING_KEY);

  const payload =
    `${base64UrlEncode(encoder.encode(JSON.stringify(JWT_HEADER)))}.` +
    `${base64UrlEncode(encoder.encode(JSON.stringify(claims)))}`;

  const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(payload));
  return `${payload}.${base64UrlEncode(new Uint8Array(signature))}`;
}

/**
 * トークンを検証してクレームを返す。
 * 署名不正・種別違い・期限切れ・形式不正はいずれも null を返す。
 */
export async function verifyToken(
  env: Env,
  token: string,
  expectedType: TokenType,
): Promise<TokenClaims | null> {
  const parts = token.split('.');
  if (parts.length !== 3) return null;

  const [encodedHeader, encodedPayload, encodedSignature] = parts;
  if (!encodedHeader || !encodedPayload || !encodedSignature) return null;

  let signature: Uint8Array;
  try {
    signature = base64UrlDecode(encodedSignature);
  } catch {
    return null;
  }

  const encoder = new TextEncoder();
  const key = await importSigningKey(env.TOKEN_SIGNING_KEY);
  const valid = await crypto.subtle.verify(
    'HMAC',
    key,
    signature,
    encoder.encode(`${encodedHeader}.${encodedPayload}`),
  );
  if (!valid) return null;

  let header: unknown;
  let claims: TokenClaims;
  try {
    header = JSON.parse(new TextDecoder().decode(base64UrlDecode(encodedHeader)));
    claims = JSON.parse(
      new TextDecoder().decode(base64UrlDecode(encodedPayload)),
    ) as TokenClaims;
  } catch {
    return null;
  }

  // 署名検証は常に HMAC で行っているので alg 混同攻撃は成立しないが、
  // 想定外のトークンを早期に弾くために念のため確認する。
  if (
    typeof header !== 'object' ||
    header === null ||
    (header as { alg?: unknown }).alg !== 'HS256'
  ) {
    return null;
  }

  if (claims.typ !== expectedType) return null;
  if (typeof claims.exp !== 'number' || claims.exp <= nowSeconds()) return null;

  return claims;
}

/** 指定した種別・有効期間のトークンを発行する。 */
export async function issueToken(
  env: Env,
  type: TokenType,
  ttlSeconds: number,
  fields: Pick<TokenClaims, 'org' | 'cid' | 'sub'> &
    Partial<Pick<TokenClaims, 'ruri' | 'nonce'>>,
): Promise<string> {
  const issuedAt = nowSeconds();
  const claims: TokenClaims = {
    typ: type,
    org: fields.org,
    cid: fields.cid,
    sub: fields.sub,
    jti: crypto.randomUUID(),
    iat: issuedAt,
    exp: issuedAt + ttlSeconds,
  };
  if (fields.ruri !== undefined) claims.ruri = fields.ruri;
  if (fields.nonce !== undefined) claims.nonce = fields.nonce;

  return signToken(env, claims);
}
