# MockOpenIdProvider E2E Tests

Playwright を使用した MockOpenIdProvider の E2E テストスイートです。

## 概要

このテストスイートは、MockOpenIdProvider の OAuth2/OpenID Connect フローを検証します。Playwright の fixture 機能とプロジェクト機能を活用し、マルチ Organization（dev/staging/production）対応のテストアーキテクチャを実現しています。

## 特徴

- **Organization Fixture**: プロジェクト名から自動的に Organization 設定を取得
- **マルチ Organization 対応**: dev/staging/production の 3 環境に対応
- **型安全**: TypeScript による型定義
- **拡張性**: 将来的な Organization 追加（dev-google, dev-line 等）が容易

## ディレクトリ構成

```
e2e-tests/
├── tests/
│   ├── common/                      # 全organization共通テスト
│   │   └── authorization_code_flow.spec.ts
│   └── organizations/               # organization固有テスト
│       ├── dev.spec.ts
│       ├── staging.spec.ts
│       └── production.spec.ts
├── fixtures/                        # カスタムfixture定義
│   └── organization.ts
├── playwright.config.ts
├── package.json
├── .env.example
├── .gitignore
└── README.md
```

## セットアップ

### 1. 環境変数設定

`.env.example` をコピーして `.env` を作成し、環境に応じて設定：

```bash
cp .env.example .env
```

**ローカル環境の場合:**
```env
MOCK_IDP_BASE_URL=https://localhost:9091
CLIENT_ID=mockclientid
CLIENT_SECRET=mock-client-secret
REDIRECT_URI=https://localhost:8081/auth/callback
TEST_USER_EMAIL=defaultuser@example.com
TEST_USER_PASSWORD=password
```

**Azure 環境の場合:**
```env
MOCK_IDP_BASE_URL=https://mock-idp.azurecontainerapps.io
CLIENT_ID=mockclientid
CLIENT_SECRET=mock-client-secret
REDIRECT_URI=https://localhost:8081/auth/callback
TEST_USER_EMAIL=defaultuser@example.com
TEST_USER_PASSWORD=password
```

### 2. 依存関係インストール

```bash
npm install
```

### 3. Playwright インストール

```bash
npx playwright install --with-deps chromium
```

## テスト実行

### 全テスト実行

```bash
npm test
```

### Organization 別実行

```bash
# dev organization のみ
npm run test:dev

# staging organization のみ
npm run test:staging

# production organization のみ
npm run test:production
```

### デバッグモード

```bash
# ヘッドありモード
npm run test:headed

# デバッグモード（ステップ実行）
npm run test:debug

# UI モード（対話的デバッグ）
npm run test:ui
```

### テスト結果確認

```bash
# レポート表示
npm run report
```

## Organization Fixture の仕組み

### 基本概念

Organization Fixture は、`testInfo.project.name` から Organization 名を自動取得し、対応する設定を提供します。

```typescript
// tests/common/authorization_code_flow.spec.ts
test('テスト名', async ({
  organization,    // "dev", "staging", "production"
  endpoints,       // { authorization, token, userinfo }
  clientId,        // Client ID
  clientSecret,    // Client Secret
  testUser,        // { email, password }
}) => {
  // organization 固有のエンドポイントを使用
  await page.goto(endpoints.authorization);
});
```

### エンドポイント URL の構成

```typescript
// dev organization の場合
endpoints.authorization = "https://localhost:9091/authorization?org=dev"
endpoints.token = "https://localhost:9091/token?org=dev"
endpoints.userinfo = "https://localhost:9091/userinfo?org=dev"
```

### Organization 固有の環境変数

各 Organization 用に固有のクライアント認証情報を設定できます：

```env
# dev organization 固有
DEV_CLIENT_ID=dev-client-id
DEV_CLIENT_SECRET=dev-client-secret

# staging organization 固有
STAGING_CLIENT_ID=staging-client-id
STAGING_CLIENT_SECRET=staging-client-secret
```

環境変数が設定されていない場合は、デフォルト値（`CLIENT_ID`、`CLIENT_SECRET`）が使用されます。

## テストケース

### 共通テスト（`tests/common/`）

すべての Organization で実行される共通のテストケース：

- `authorization_code_flow.spec.ts`: OAuth2 認可コードフロー
  - 認可エンドポイントへのアクセス
  - トークンエンドポイントでアクセストークン・リフレッシュトークン取得
  - ユーザー情報エンドポイントでユーザー情報取得
  - リフレッシュトークンでの新規アクセストークン取得

### Organization 固有テスト（`tests/organizations/`）

各 Organization に固有のテストケース（将来的な拡張用）：

- `dev.spec.ts`: dev organization 固有のテスト
- `staging.spec.ts`: staging organization 固有のテスト
- `production.spec.ts`: production organization 固有のテスト

## CI/CD（GitHub Actions）

GitHub Actions ワークフロー（`.github/workflows/e2e-tests.yml`）により、以下が自動実行されます：

1. SQL Server コンテナ起動
2. MockOpenIdProvider ビルド・マイグレーション・起動
3. E2E テスト実行（全 Organization）
4. テスト結果アップロード（失敗時）

## トラブルシューティング

### HTTPS 証明書エラー

`playwright.config.ts` で `ignoreHTTPSErrors: true` が設定されているため、自己署名証明書でも動作します。

### テストタイムアウト

デフォルトのタイムアウトは 10 秒です。変更する場合は `playwright.config.ts` の `actionTimeout` を調整してください。

### ログ確認

失敗したテストのスクリーンショット・動画は `test-results/` に保存されます。

## 拡張性

### 新しい Organization の追加

1. `playwright.config.ts` に新しいプロジェクトを追加：
   ```typescript
   {
     name: 'dev-google',
     testMatch: ['**/common/**/*.spec.ts', '**/organizations/dev-google.spec.ts'],
     use: { ...devices['Desktop Chrome'] },
   }
   ```

2. Organization 固有テストファイルを作成：
   ```bash
   touch tests/organizations/dev-google.spec.ts
   ```

3. 固有の環境変数を設定（必要に応じて）：
   ```env
   DEV_GOOGLE_CLIENT_ID=dev-google-client-id
   DEV_GOOGLE_CLIENT_SECRET=dev-google-client-secret
   ```

### カスタム Fixture の追加

`fixtures/organization.ts` に新しい Fixture を追加できます：

```typescript
export const test = base.extend<OrganizationFixture & CustomFixture>({
  customProperty: async ({}, use) => {
    const customValue = 'some value';
    await use(customValue);
  },
});
```

## 参考資料

- [Playwright Documentation](https://playwright.dev/)
- [Playwright Fixtures](https://playwright.dev/docs/test-fixtures)
- [Playwright Projects](https://playwright.dev/docs/test-projects)
