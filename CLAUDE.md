# CLAUDE.md

このファイルは、Claude Code が EcAuth.MockIdP コードベースを操作する際のガイダンスを提供します。
日本語で回答してください。

## プロジェクト概要

EcAuth.MockIdP は、EcAuth Identity Provider の E2E テスト用に設計された Mock OpenID Connect
Provider です。制御された環境で外部 IdP（Google、LINE、Facebook など）をシミュレートします。

**目的**:

- EcAuth IdentityProvider の E2E テスト
- 開発・ステージング環境でのテスト
- 本番データからの分離
- 運用コストゼロ（Cloudflare Workers 無料枠）

**v2.0.0 で ASP.NET Core + Azure SQL Database から Cloudflare Workers + TypeScript へ全面移行しました。**
旧実装は `v1-dotnet-final` タグで参照できます。

## 技術スタック

- **ランタイム**: Cloudflare Workers
- **言語**: TypeScript 7（`tsc --noEmit` で型検査のみ。ビルドは wrangler/esbuild）
- **フレームワーク**: Hono
- **ストレージ**: Workers KV（認可コードの単回使用マーカーのみ）
- **テスト**: Vitest + `@cloudflare/vitest-pool-workers`（ユニット）、Playwright（E2E）
- **パッケージマネージャ**: pnpm（ワークスペース構成）

## 設計の要点

### ステートレス設計

認可コード・アクセストークン・リフレッシュトークンはすべて **HS256 署名付きの自己完結トークン**
（JWT 形式）で、データベースに保存しません。必要な情報はクレームに埋め込みます。

```
typ  トークン種別 (code / at / rt)
org  発行元テナント        ← テナントをまたいだ利用を防ぐ
cid  client_id
sub  ユーザー識別子
jti  トークン固有 ID       ← 認可コードの単回使用判定に使う
iat / exp
ruri 認可時の redirect_uri（code のみ）
nonce                      （code のみ）
```

**永続化が必要なのは「認可コードが使用済みかどうか」だけ**で、これは KV に `jti` を TTL 付きで
書き込むことで担保しています。KV は結果整合のため厳密な排他ではありませんが、モック用途では
十分と判断しています。厳密性が必要になったら Durable Objects を検討してください。

### テナント設定

`organization` / `client` / `mock_idp_user` の 3 テーブルは廃止し、Workers Secret の
`MOCKIDP_{DEV,STAGING,PRODUCTION}_*` に置き換えました。命名は .NET 版の
`EnvironmentClientUserSeeder` と 1Password のアイテム構成をそのまま引き継いでいます。

**ユーザーはテナントごとに 1 人ですが、クライアントは複数持てます**（既定 + `FEDERATE_` 接頭辞の
2 件）。EcAuth のフェデレーション E2E は `defaultuser@example.com` でログインしつつ
`federateclientid` でトークンを交換するため、dev テナントには 2 件目が必須です。これは旧 .NET 版で
`InsertFederateClient` マイグレーションが作っていたクライアントに相当します。
**1 テナント 1 クライアントに戻すと EcAuth 側の
`federate_authorization_code_flow.spec.ts` が壊れます。**

**テナント設定を `wrangler.jsonc` の `vars` に書かないこと。** 本リポジトリは公開されており、
staging / production の `redirect_uri` は EcAuth のデプロイ先 URL を含みます
（ルート CLAUDE.md の「Azure にデプロイしたエンドポイントの URL を載せない」方針）。

### `sub` の導出

`sha256("{org}:{email}")` の先頭 32 桁から決定的に導出します。EcAuth 側では
`ExternalIdpMapping.ExternalSubject` として保存され、JIT プロビジョニングのキーになるため、
**同じユーザーなら常に同じ値である必要があります**。

.NET 版は DB の連番 ID（`mock_idp_user.id`）を返していたため、移行によって `sub` が変わります。
旧値に合わせる必要がある場合は `MOCKIDP_{ENV}_USER_SUBJECT` で明示的に上書きしてください。

### パスワード検証

Workers Free プランの **CPU 制限は 1 リクエストあたり 10ms** です。ASP.NET Identity の
`PasswordHasher`（PBKDF2 10 万イテレーション）はこれを超えるため採用していません。

MockIdP の認証情報は 1Password から注入されるテスト用の固定値であり、ユーザーが登録した秘密を
保管しているわけではないので、`src/compare.ts` の定数時間比較で十分です。
**「セキュリティ向上のため」と称してハッシュ化に戻さないこと。**

## .NET 版からの意図的な差分

移植にあたって挙動を変えた箇所です。EcAuth 側との契約に関わるため、戻す前に影響を確認してください。

| 項目 | .NET 版 | Workers 版 | 理由 |
|---|---|---|---|
| `redirect_uri` 不一致時 | 要求された URL へエラー付きでリダイレクト | **400 を返す** | オープンリダイレクトの回避。RFC 6749 §4.1.2.1 準拠 |
| トークン要求の `redirect_uri` | 非空チェックのみ | 認可時の値と一致を検証 | RFC 6749 §4.1.3 準拠。EcAuth は両方で同じ値を送るため安全 |
| `GET /userinfo/me` | テストユーザーの email を返す | **廃止** | 利用箇所が無く、公開エンドポイントで email を晒す必要がない |
| `sub` の値 | DB の連番 int | `sha256(org:email)` の先頭 32 桁 | ステートレス化。`*_USER_SUBJECT` で上書き可 |
| `state` / `nonce` の echo | 常に付与（空でも） | 指定された場合のみ付与 | 空パラメータを送らない |
| ヘルスチェック | DB 疎通を確認 | 常に 200 | 起動時に接続する外部依存が無い |
| 未設定テナント | organization 行があれば通過 | `invalid_organization` | 設定不足を早期に検出する |

**維持している .NET 版の挙動**:

- トークンエンドポイントのエラーは **HTTP 200 + `{"error": "..."}`**（RFC 6749 §5.2 は 400 を
  求めるが、既存挙動を優先）
- リフレッシュトークンはローテーションせず、同じ値を返す
- 認可コードの有効期間は 1 時間
- `/authorization` はログイン画面を出さず Basic 認証で即リダイレクト
  （EcAuth の E2E が Playwright の `httpCredentials` に依存している）

## 開発コマンド

```bash
# 依存インストール（ワークスペースなので e2e-tests の分も入る）
pnpm install

# ローカル用バインディングを用意（テスト値のみ。1Password 不要）
cp .dev.vars.example .dev.vars

# 起動（http://localhost:8787）
pnpm dev

# 型チェック
pnpm run typecheck

# ユニットテスト
pnpm test

# 特定のテストファイルのみ
pnpm exec vitest run test/tenancy.test.ts
```

## プロジェクト構造

```
EcAuth.MockIdP/
├── src/
│   ├── index.ts              # ルーティング + テナント解決ミドルウェア
│   ├── env.ts                # バインディング型と env 読み出しヘルパー
│   ├── tenants.ts            # テナント解決・sub の導出
│   ├── tokens.ts             # HS256 署名トークンの発行・検証
│   ├── compare.ts            # 定数時間比較
│   ├── types.ts              # Hono の型引数
│   └── routes/
│       ├── authorization.ts
│       ├── token.ts
│       └── userinfo.ts
├── test/                     # Vitest（Workers ランタイム上で実行）
├── e2e-tests/                # Playwright（ワークスペースメンバー）
├── scripts/collect-secrets.mjs
├── wrangler.jsonc
├── .env.workers.tpl          # Workers Secret 投入用 1Password テンプレート
├── .env.cloudflare.tpl       # wrangler の認証情報（デプロイ用）
└── .dev.vars.example         # ローカル用（平文。意図的）
```

**`.env.workers.tpl` と `.env.cloudflare.tpl` を統合しないこと。** 前者は Worker に注入する
シークレット、後者は Cloudflare API の認証情報で、混ぜると API トークンを Worker Secret として
公開してしまう。

wrangler は `wrangler login` ではなく 1Password 経由で認証する。

```bash
op run --env-file=.env.cloudflare.tpl -- pnpm exec wrangler <command>
```

## pnpm ワークスペース

ルート（Worker 本体）と `e2e-tests/`（Playwright）の 2 パッケージ構成です。
**`e2e-tests/` 側に個別の lockfile を置かないでください。** ルートに
`pnpm-workspace.yaml` がある状態で `e2e-tests` を独立パッケージとして扱うと、
pnpm がワークスペース root を親と解釈して `node_modules` を作り直そうとし、
`ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY` で失敗します。

依存の追加はルートから行います。

```bash
pnpm add -D <pkg>                              # Worker 本体
pnpm --filter ecauth-mockidp-e2e-tests add -D <pkg>   # E2E
```

## CI/CD

| ワークフロー | 内容 |
|---|---|
| `.github/workflows/ci.yml` | 型チェック + Vitest |
| `.github/workflows/e2e-tests.yml` | `wrangler dev` を起動して Playwright E2E |
| `.github/workflows/deploy.yml` | `main` push で `wrangler deploy` + ヘルスチェック |

**Workers Secret は CI で投入しません。** `wrangler deploy` はシークレットを上書きしないため、
値を変えるときだけ README の「シークレットの投入」をローカルから実行します。

## EcAuth 側との連携

EcAuth は `open_id_provider` テーブルに MockIdP のエンドポイントを保持しています。

- **`OrganizationClientSeeder.SeedOpenIdProviderAsync` は既存行を更新しません**
  （同名レコードがあれば `return false`）。エンドポイントを変更するときは、
  環境変数を変えるだけでは反映されないため、EcAuth 側に UPDATE マイグレーションが必要です。
- EcAuth は認可リクエストとトークンリクエストの両方で
  `DEFAULT_ORGANIZATION_REDIRECT_URI` を送ります。
- EcAuth は `state` を送りますが `nonce` は送りません。
- EcAuth は `id_token` を使わず、`/userinfo` から `sub` を取得します。

## コーディング規約

- 行末の空白を削除
- 改行コードは LF
- 日本語コメント・ドキュメント可
- セキュリティ脆弱性（オープンリダイレクト、タイミング攻撃など）に注意

## トラブルシューティング

### `wrangler dev` が起動しない

`.dev.vars` があるか確認してください（`cp .dev.vars.example .dev.vars`）。

### デプロイ後に `invalid_organization` が返る

対象テナントの Workers Secret が揃っていません。必須項目
（`CLIENT_ID` / `CLIENT_SECRET` / `REDIRECT_URI` / `USER_EMAIL` / `USER_PASSWORD`）が
1 つでも欠けていると未設定扱いになります。

```bash
pnpm exec wrangler secret list
```

### トークン検証がすべて失敗する

`TOKEN_SIGNING_KEY` を変更すると、それ以前に発行されたトークンはすべて無効になります。
デプロイ直後に既存のトークンが弾かれるのは想定どおりです。
