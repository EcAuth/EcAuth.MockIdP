# EcAuth.MockIdP

Mock OpenID Provider for E2E testing with EcAuth Identity Provider.

EcAuth Identity Provider の E2E テスト専用に作られたモック OpenID Connect Provider です。
外部 IdP（Google / LINE / Facebook 等）を制御された環境でシミュレートします。

## 特徴

- **ステートレス**: 認可コード・アクセストークン・リフレッシュトークンは HS256 署名付きの
  自己完結トークンで、データベースを持たない
- **マルチテナント**: `?org=` / `X-Organization` による Organization 単位の論理分離
- **Cloudflare Workers**: 無料枠（10 万リクエスト/日）に収まり、**運用コストゼロ**
- **コールドスタートなし**: スケールゼロからの起動待ちがない

> **注**: v2.0.0 で ASP.NET Core + Azure SQL Database から Cloudflare Workers へ全面移行しました。
> 旧実装は `v1-dotnet-final` タグを参照してください。

## アーキテクチャ

```text
リクエスト
  ↓
テナント解決ミドルウェア (?org= → X-Organization → 既定 dev)
  ↓
テナント設定 (Workers Secret: MOCKIDP_{DEV,STAGING,PRODUCTION}_*)
  ↓
各エンドポイント
  ↓
HS256 署名トークン（永続化なし）
  + KV (USED_CODES): 使用済み認可コードのマーカーのみ
```

Organization ごとに **User 1 人・Client 1〜2 件**（既定 + 任意の federate）を持つ構成で、
すべて設定値から解決します。テーブルもマイグレーションも存在しません。

### 認可コードの再利用防止について

使用済みの認可コードは KV に `jti` を TTL 付きで記録して弾きます。これにより
**逐次的な再利用**（一度使ったコードを後から使い回す）は防げます。

一方 Cloudflare KV は結果整合で、`get` → `put` は原子的な read-modify-write では
ありません。そのため**同一コードを別エッジから同時に交換された場合、複数が成功しうる**
という制約があります。厳密な排他が必要になったら Durable Objects へ移す必要があります
が、モック IdP の用途（EcAuth も E2E も 1 回しか交換しない）では過剰と判断しています。

## エンドポイント

| メソッド | パス | 説明 |
|---|---|---|
| GET | `/authorization` | HTTP Basic 認証 → `redirect_uri` へ認可コード付きでリダイレクト |
| POST | `/token` | `authorization_code` / `refresh_token` グラント |
| GET | `/userinfo` | Bearer トークン → `{ "sub": "..." }` |
| GET | `/healthz`, `/healthz/ready`, `/healthz/live` | ヘルスチェック |

いずれも `?org={dev,staging,production}` を受け付けます（省略時は `dev`）。

### 認可エンドポイント

```http
GET /authorization?org=dev
  &response_type=code
  &client_id={client_id}
  &redirect_uri={redirect_uri}
  &scope={scope}
  &state={state}
  &nonce={nonce}
Authorization: Basic base64(email:password)
```

ログイン画面は持たず、Basic 認証が通ればそのままリダイレクトします。

### トークンエンドポイント

```http
POST /token?org=dev
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&code={authorization_code}
&redirect_uri={redirect_uri}
&client_id={client_id}
&client_secret={client_secret}
```

```http
POST /token?org=dev
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token
&refresh_token={refresh_token}
&client_id={client_id}
&client_secret={client_secret}
```

### UserInfo エンドポイント

```http
GET /userinfo?org=dev
Authorization: Bearer {access_token}
```

`sub` は `sha256("{org}:{email}")` の先頭 32 桁から決定的に導出されます。
同じテナント・同じユーザーなら常に同じ値になります。明示指定したい場合は
`MOCKIDP_{ENV}_USER_SUBJECT` を設定してください。

## セットアップ

### 前提

- Node.js 24 以上
- pnpm 11 以上
- Cloudflare アカウント（デプロイする場合のみ）

### ローカル開発

```bash
git clone https://github.com/EcAuth/EcAuth.MockIdP.git
cd EcAuth.MockIdP

# 依存インストール（pnpm ワークスペースなので e2e-tests の分も入る）
pnpm install

# ローカル用のバインディングを用意（テスト値のみ。1Password 不要）
cp .dev.vars.example .dev.vars

# 起動
pnpm dev
```

`http://localhost:8787` で待ち受けます。

```bash
curl http://localhost:8787/healthz
# {"status":"healthy"}
```

### ビルド・テスト

```bash
pnpm run typecheck   # tsc --noEmit
pnpm test            # vitest (Workers ランタイム上で実行)
```

## デプロイ

### wrangler の認証

`wrangler login` の対話ログインの代わりに、1Password から API トークンを注入します。

```bash
eval $(op signin)
op run --env-file=.env.cloudflare.tpl -- pnpm exec wrangler <command>
```

`.env.cloudflare.tpl` は **デプロイ用の認証情報**（`CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID`）
のテンプレートで、Worker に注入するシークレットの `.env.workers.tpl` とは別物です。混ぜると
Cloudflare の API トークンを Worker Secret として公開してしまうため、統合しないでください。

### 初回セットアップ（構築済み。再構築時のみ）

1. **KV namespace**

   ```bash
   op run --env-file=.env.cloudflare.tpl -- pnpm exec wrangler kv namespace create USED_CODES
   ```

   出力された id を `wrangler.jsonc` の `kv_namespaces[0].id` に反映してコミットします。
   KV namespace ID は秘匿情報ではありません。

2. **トークン署名鍵**

   1Password の `EcAuth` 保管庫のアイテム `mockidp-workers` / フィールド `token_signing_key`
   に保存されています。ローテーションする場合は値を差し替えてシークレットを再投入します
   （既存のトークンはすべて無効になります）。

3. **GitHub Secrets**

   | シークレット | 用途 |
   |---|---|
   | `CLOUDFLARE_API_TOKEN` | `wrangler deploy` 用の API トークン |
   | `CLOUDFLARE_ACCOUNT_ID` | デプロイ先アカウント ID |

   値は 1Password の `cloudflare-credentials` アイテムと同じです。

### シークレットの投入

テナント設定と署名鍵は Workers Secret として保持します。**デプロイでは上書きされない**ため、
値を変更するときだけ以下を実行します（CI では投入しません）。

```bash
eval $(op signin)

op run --env-file=.env.workers.tpl -- node scripts/collect-secrets.mjs \
  | pnpm exec wrangler secret bulk
```

`op run` は復号値をサブプロセスの環境変数にだけ渡し、パイプでそのまま wrangler に流すため、
平文がディスクに残りません。`op inject` でファイルを生成しないでください。

投入されるキーは以下のとおりです。

| キー | 説明 |
|---|---|
| `TOKEN_SIGNING_KEY` | HS256 署名鍵 |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_CLIENT_ID` | OAuth2 クライアント ID |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_CLIENT_SECRET` | クライアントシークレット |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_CLIENT_NAME` | 表示名（省略可） |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_REDIRECT_URI` | 登録済みリダイレクト URI |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_USER_EMAIL` | テストユーザーのメールアドレス |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_USER_PASSWORD` | テストユーザーのパスワード |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_USER_SUBJECT` | `sub` の明示指定（省略可） |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_FEDERATE_CLIENT_ID` | 2 件目のクライアント ID（省略可） |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_FEDERATE_CLIENT_SECRET` | 同シークレット |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_FEDERATE_CLIENT_NAME` | 同表示名（省略可） |
| `MOCKIDP_{DEV,STAGING,PRODUCTION}_FEDERATE_REDIRECT_URI` | 同リダイレクト URI（省略時は既定クライアントの値） |

必須項目（`CLIENT_ID` / `CLIENT_SECRET` / `REDIRECT_URI` / `USER_EMAIL` / `USER_PASSWORD`）が
1 つでも欠けているテナントは「未設定」とみなされ、`invalid_organization` を返します。

### テナントあたりのクライアントは複数持てる

ユーザーはテナントごとに 1 人ですが、**クライアントは既定 + federate の 2 件**を登録できます。
EcAuth のフェデレーション E2E は `MOCKIDP_DEV_USER_EMAIL` でログインしつつ
`MOCKIDP_DEV_FEDERATE_CLIENT_ID` でトークンを交換するため、dev テナントでは両方が必要です
（旧 .NET 版の `InsertFederateClient` マイグレーションが作っていたクライアントに相当します）。

### デプロイ

`main` への push で `.github/workflows/deploy.yml` が `wrangler deploy` を実行します。
手動デプロイは以下のとおりです。

```bash
op run --env-file=.env.cloudflare.tpl -- pnpm exec wrangler deploy
```

切り戻しは `wrangler rollback` で直前バージョンに戻せます。

## コスト

Cloudflare の無料枠内で運用でき、**月額 0 円**です。

| リソース | 無料枠 | 用途 |
|---|---|---|
| Workers | 10 万リクエスト/日、CPU 10ms/リクエスト | 本体 |
| Workers KV | 10 万 reads/日、1,000 writes/日 | 使用済み認可コードのマーカー |

認可コード 1 回の引き換えにつき KV への書き込みが 1 回発生します。

## E2E テスト

Playwright による E2E テストは `e2e-tests/` にあります。詳細は
[e2e-tests/README.md](./e2e-tests/README.md) を参照してください。

```bash
# ローカルの Worker を起動しておく
pnpm dev

# 別ターミナルで
cd e2e-tests
pnpm exec playwright install --with-deps chromium
pnpm test
```

## ライセンス

MIT License - [LICENSE](LICENSE) を参照してください。

## 関連リポジトリ

- [EcAuth](https://github.com/EcAuth/EcAuth): Identity Provider 本体
- [ecauth-infrastructure](https://github.com/EcAuth/ecauth-infrastructure): IaC（Terraform + Ansible）
