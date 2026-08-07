/**
 * 文字列の定数時間比較。
 *
 * 両者の SHA-256 ダイジェスト（常に 32 バイト）同士を比較することで、
 * `crypto.subtle.timingSafeEqual` の「同じバイト長でなければ例外」という制約を
 * 回避しつつ、入力長の差もタイミングから漏れないようにする。
 *
 * .NET 版は ASP.NET Identity の `PasswordHasher`（PBKDF2 10 万イテレーション）を
 * 使っていたが、Workers Free プランの CPU 制限（10ms/リクエスト）を超えるため
 * 採用しない。MockIdP の認証情報は 1Password から注入されるテスト用の固定値で、
 * ハッシュ保存する対象（ユーザーが登録した秘密）ではないため、平文の設定値との
 * 定数時間比較で十分。
 */
export async function safeEqual(a: string, b: string): Promise<boolean> {
  const encoder = new TextEncoder();
  const [digestA, digestB] = await Promise.all([
    crypto.subtle.digest('SHA-256', encoder.encode(a)),
    crypto.subtle.digest('SHA-256', encoder.encode(b)),
  ]);
  return crypto.subtle.timingSafeEqual(digestA, digestB);
}
