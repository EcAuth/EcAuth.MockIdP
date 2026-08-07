import { env } from 'cloudflare:workers';
import app from '../src/index';
import type { Env } from '../src/env';

export const testEnv = env as unknown as Env;

/** テスト用テナントの認証情報（vitest.config.ts の bindings と対応）。 */
export const DEV = {
  org: 'dev',
  clientId: 'mockclientid',
  clientSecret: 'mock-client-secret',
  redirectUri: 'https://localhost:8081/v1/auth/callback',
  email: 'defaultuser@example.com',
  password: 'password',
} as const;

/**
 * dev テナントの 2 件目のクライアント。
 * ユーザーと redirect_uri は既定クライアントと共有する。
 */
export const DEV_FEDERATE = {
  org: 'dev',
  clientId: 'federateclientid',
  clientSecret: 'federate-client-secret',
  redirectUri: 'https://localhost:8081/v1/auth/callback',
  email: 'defaultuser@example.com',
  password: 'password',
} as const;

export const STAGING = {
  org: 'staging',
  clientId: 'staging-mockclientid',
  clientSecret: 'staging-mock-client-secret',
  redirectUri: 'https://staging.example.test/v1/auth/callback',
  email: 'staging-user@example.com',
  password: 'staging-password',
} as const;

/** テストで使うクライアント資格情報の形。 */
export type TestClient = {
  readonly org: string;
  readonly clientId: string;
  readonly clientSecret: string;
  readonly redirectUri: string;
  readonly email: string;
  readonly password: string;
};

const ORIGIN = 'https://mockidp.test';

export function basicAuth(email: string, password: string): string {
  return `Basic ${btoa(`${email}:${password}`)}`;
}

export async function request(path: string, init?: RequestInit): Promise<Response> {
  return await app.fetch(new Request(`${ORIGIN}${path}`, init), testEnv);
}

/** 認可エンドポイントを叩き、Location から認可コードを取り出す。 */
export async function getAuthorizationCode(
  tenant: TestClient,
  extraQuery: Record<string, string> = {},
): Promise<string> {
  const params = new URLSearchParams({
    org: tenant.org,
    client_id: tenant.clientId,
    redirect_uri: tenant.redirectUri,
    response_type: 'code',
    scope: 'openid profile email',
    ...extraQuery,
  });

  const response = await request(`/authorization?${params.toString()}`, {
    headers: { Authorization: basicAuth(tenant.email, tenant.password) },
  });

  if (response.status !== 302) {
    throw new Error(`expected 302 but got ${response.status}: ${await response.text()}`);
  }

  const location = response.headers.get('location');
  if (!location) throw new Error('no location header');

  const code = new URL(location).searchParams.get('code');
  if (!code) throw new Error(`no code in location: ${location}`);
  return code;
}

/** トークンエンドポイントに form-urlencoded で POST する。 */
export function postToken(org: string, fields: Record<string, string>): Promise<Response> {
  return request(`/token?org=${org}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams(fields).toString(),
  });
}

/** 認可コードをアクセストークン・リフレッシュトークンに交換する。 */
export async function exchangeCode(
  tenant: TestClient,
  code: string,
): Promise<{ access_token: string; refresh_token: string; token_type: string; expires_in: number }> {
  const response = await postToken(tenant.org, {
    grant_type: 'authorization_code',
    code,
    redirect_uri: tenant.redirectUri,
    client_id: tenant.clientId,
    client_secret: tenant.clientSecret,
  });
  return response.json();
}
