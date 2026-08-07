/**
 * Worker に注入されるバインディングと設定。
 *
 * テナント設定は `MOCKIDP_{DEV,STAGING,PRODUCTION}_*` という平坦な名前で
 * Workers Secret として注入されるため、インデックスシグネチャで受ける。
 * この命名は .NET 版 (EnvironmentClientUserSeeder) および 1Password の
 * アイテム構成をそのまま引き継いだもの。
 */
export interface Env {
  /**
   * 認可コードの単回使用を担保する KV。
   * 保存するのは使用済みマーカー（jti → "1"）のみで、機密情報は入れない。
   */
  USED_CODES: KVNamespace;

  /** トークン署名鍵（HS256）。Workers Secret として注入する。 */
  TOKEN_SIGNING_KEY: string;

  [key: string]: unknown;
}

/**
 * env から文字列値を読み出す。未設定・空文字・非文字列は undefined を返す。
 */
export function readEnv(env: Env, key: string): string | undefined {
  const value = env[key];
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}
