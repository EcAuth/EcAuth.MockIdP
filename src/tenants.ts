import { type Env, readEnv } from './env';

/**
 * サポートする Organization（テナント）。
 *
 * .NET 版では `organization` テーブルの行として管理していたが、Workers 版では
 * 設定のみで完結するため定数として持つ。
 */
export const KNOWN_ORGS = ['dev', 'staging', 'production'] as const;

export type OrgName = (typeof KNOWN_ORGS)[number];

/** Organization ごとの環境変数プレフィックス。 */
const ENV_PREFIX: Record<OrgName, string> = {
  dev: 'MOCKIDP_DEV',
  staging: 'MOCKIDP_STAGING',
  production: 'MOCKIDP_PRODUCTION',
};

/** OAuth2 クライアント 1 件ぶんの設定。 */
export interface ClientConfig {
  clientId: string;
  clientSecret: string;
  clientName: string;
  redirectUri: string;
}

/**
 * 1 テナントぶんの設定。
 *
 * .NET 版の `Organization` / `Client` / `MockIdpUser` を 1 つにまとめたもの。
 * ユーザーはテナントごとに 1 人だが、**クライアントは複数持てる**。
 * EcAuth のフェデレーション E2E は `defaultuser@example.com` でログインしつつ
 * federate クライアント（旧 MockIdP の InsertFederateClient マイグレーションが
 * 作っていたもの）でトークンを交換するため、2 件目が必要になる。
 */
export interface Tenant {
  org: OrgName;
  clients: ClientConfig[];
  userEmail: string;
  userPassword: string;
  /** `/userinfo` が返す `sub`。EcAuth 側では ExternalIdpMapping のキーになる。 */
  userSubject: string;
}

export function isKnownOrg(value: string): value is OrgName {
  return (KNOWN_ORGS as readonly string[]).includes(value);
}

/** client_id に対応するクライアント設定を返す。 */
export function findClient(tenant: Tenant, clientId: string): ClientConfig | undefined {
  return tenant.clients.find((client) => client.clientId === clientId);
}

/**
 * `sub` を org と email から決定的に導出する。
 *
 * ステートレス化により DB の連番 ID が無くなったため、同じテナント・同じ
 * ユーザーなら常に同じ値になる導出を使う。旧 .NET 版の `mock_idp_user.id` に
 * 合わせたい場合は `MOCKIDP_{ENV}_USER_SUBJECT` で明示的に上書きする。
 */
export async function deriveSubject(org: string, email: string): Promise<string> {
  const data = new TextEncoder().encode(`${org}:${email}`);
  const digest = await crypto.subtle.digest('SHA-256', data);
  return [...new Uint8Array(digest)]
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
    .slice(0, 32);
}

/**
 * クライアント設定を環境変数から読む。
 *
 * @param infix 空文字なら既定クライアント（`MOCKIDP_DEV_CLIENT_ID`）、
 *              `FEDERATE_` なら federate クライアント（`MOCKIDP_DEV_FEDERATE_CLIENT_ID`）。
 * @param fallbackRedirectUri redirect_uri が個別指定されていない場合に使う値。
 */
function readClient(
  env: Env,
  prefix: string,
  infix: string,
  defaultName: string,
  fallbackRedirectUri?: string,
): ClientConfig | null {
  const clientId = readEnv(env, `${prefix}_${infix}CLIENT_ID`);
  const clientSecret = readEnv(env, `${prefix}_${infix}CLIENT_SECRET`);
  const redirectUri = readEnv(env, `${prefix}_${infix}REDIRECT_URI`) ?? fallbackRedirectUri;

  if (!clientId || !clientSecret || !redirectUri) return null;

  return {
    clientId,
    clientSecret,
    clientName: readEnv(env, `${prefix}_${infix}CLIENT_NAME`) ?? defaultName,
    redirectUri,
  };
}

/**
 * 環境変数からテナント設定を解決する。
 * 既定クライアントかユーザー情報が欠けている場合は「未設定テナント」として null を返す。
 */
export async function resolveTenant(env: Env, org: OrgName): Promise<Tenant | null> {
  const prefix = ENV_PREFIX[org];

  const primary = readClient(env, prefix, '', `${org}Client`);
  const userEmail = readEnv(env, `${prefix}_USER_EMAIL`);
  const userPassword = readEnv(env, `${prefix}_USER_PASSWORD`);

  if (!primary || !userEmail || !userPassword) return null;

  // federate クライアントは任意。redirect_uri は既定クライアントの値を引き継ぐ。
  const federate = readClient(env, prefix, 'FEDERATE_', 'FederateClient', primary.redirectUri);

  return {
    org,
    clients: federate ? [primary, federate] : [primary],
    userEmail,
    userPassword,
    userSubject:
      readEnv(env, `${prefix}_USER_SUBJECT`) ?? (await deriveSubject(org, userEmail)),
  };
}
