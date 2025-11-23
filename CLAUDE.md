# CLAUDE.md

このファイルは、Claude Code が EcAuth.MockIdP コードベースを操作する際のガイダンスを提供します。
日本語で回答してください。

## プロジェクト概要

EcAuth.MockIdP は、EcAuth Identity Provider の E2E テスト用に設計された Mock OpenID Connect Provider です。制御された環境で外部 IdP（Google、LINE、Facebook など）をシミュレートします。

**目的**:
- EcAuth IdentityProvider の E2E テスト
- 開発・ステージング環境でのテスト
- 本番データからの分離
- コスト最適化された Azure デプロイ（月額約¥75）

## アーキテクチャ

### マルチテナント設計

```
OrganizationMiddleware (リクエストから Organization を抽出)
  ↓
OrganizationService (現在の Organization コンテキストを保持)
  ↓
IdpDbContext (グローバルクエリフィルターを適用)
  ↓
全エンティティが自動的に OrganizationId でフィルタリング
```

- **OrganizationMiddleware**: クエリパラメータ `?org=` またはヘッダー `X-Organization` から organization を抽出
- **OrganizationService**: 現在の organization コンテキストを保持する Scoped サービス
- **グローバルクエリフィルター**: 全データベースクエリに自動適用

### エンティティモデル

```
Organization (テナント)
  ├─ MockIdpUser (ユーザー)
  ├─ Client (OAuth2 クライアント)
  ├─ AuthorizationCode (認可コード)
  ├─ AccessToken (アクセストークン)
  └─ RefreshToken (リフレッシュトークン)
```

`Organization` 以外の全エンティティは `OrganizationId` 外部キーを持ち、現在の organization でフィルタリングされます。

### OAuth2/OIDC フロー

```
/authorization?org=dev
  → 認可コード生成
  → クライアントにコード付きでリダイレクト

/token?org=dev
  → 認可コード検証
  → アクセストークン + ID トークン生成
  → トークン返却

/userinfo?org=dev
  → アクセストークン検証
  → ユーザー情報返却（subject、name、email）
```

## 開発コマンド

### セットアップ

```bash
# リポジトリクローン
git clone https://github.com/EcAuth/EcAuth.MockIdP.git
cd EcAuth.MockIdP

# GitHub Packages 認証設定
echo "protocol=https
host=github.com" | gh auth git-credential get | awk -F= '/username/ {u=$2} /password/ {p=$2} END {system("dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json --name github --username " u " --password " p " --store-password-in-clear-text")}'

# 環境設定
cp .env.dist .env
# .env を編集してデータベース接続文字列を設定
```

### GitHub Packages 認証

このプロジェクトは GitHub Packages から `EcAuth.IdpUtilities` NuGet パッケージを取得するため、認証設定が必要です。

#### ローカル開発環境

上記の `gh auth git-credential` コマンドで自動設定されます。手動で設定する場合：

```bash
# GitHub Personal Access Token を使用
dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json \
  --name github \
  --username <your-github-username> \
  --password <your-github-token> \
  --store-password-in-clear-text
```

#### GitHub Actions 環境

**Organization secrets として `ORG_PAT` を使用（推奨）:**

```yaml
- name: Add GitHub Packages source with credentials
  run: |
    dotnet nuget remove source github || true
    dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json \
      --name github \
      --username ${{ github.actor }} \
      --password ${{ secrets.ORG_PAT || secrets.PACKAGES_READ_TOKEN }} \
      --store-password-in-clear-text
```

**設定されているシークレット:**
- `ORG_PAT`: Organization レベルのトークン（**推奨**、今後はこちらを使用）
- `PACKAGES_READ_TOKEN`: 後方互換性のために残している従来のトークン

**Docker ビルド時:**

```yaml
- name: Build Docker image
  run: |
    docker build --build-arg GITHUB_TOKEN=${{ secrets.ORG_PAT || secrets.PACKAGES_READ_TOKEN }} \
      -t mock-idp:latest \
      -f src/MockOpenIdProvider/Dockerfile .
```

**必要なスコープ:**
- `read:packages`: GitHub Packages からのパッケージ読み取り

### ビルドとテスト

```bash
# ソリューションビルド
dotnet build EcAuth.MockIdP.sln

# ユニットテスト実行
dotnet test

# 特定のテストクラス実行
dotnet test --filter ClassName=TokenControllerTests
```

### E2E テスト

Playwright を使用した E2E テストスイートが `e2e-tests/` ディレクトリに配置されています。

```bash
cd e2e-tests

# 依存関係インストール
npm install

# Playwright インストール
npx playwright install --with-deps chromium

# 環境変数設定
cp .env.example .env
# .env を編集して環境を設定

# 全テスト実行
npm test

# Organization 別実行
npm run test:dev
npm run test:staging
npm run test:production

# デバッグモード
npm run test:debug
npm run test:ui
```

**Docker Compose 環境でのテスト:**

```bash
# Docker Compose を起動
docker compose up -d

# E2E テストを実行
cd e2e-tests
export MOCK_IDP_BASE_URL=https://localhost:9091
npm test
```

詳細は @e2e-tests/README.md を参照してください。

### データベース操作

```bash
# マイグレーション追加
cd src/MockOpenIdProvider
dotnet ef migrations add <MigrationName>

# マイグレーション適用
export $(cat ../../.env | grep -v '^#' | xargs)
dotnet ef database update

# 最後のマイグレーション削除（未適用の場合）
dotnet ef migrations remove
```

### Docker

#### Docker イメージのビルド

```bash
# リポジトリルートから実行
docker build --build-arg GITHUB_TOKEN=<your_token> -t mock-idp:latest -f src/MockOpenIdProvider/Dockerfile .

# コンテナ起動
docker run -d -p 8080:8080 -p 8081:8081 \
  -e ConnectionStrings__MockIdpDbContext="Server=..." \
  mock-idp:latest
```

#### Docker Compose での起動

Docker Compose を使用すると、SQL Server と MockOpenIdProvider を同時に起動できます。

```bash
# .env ファイルを確認（GITHUB_TOKEN が設定されていること）
cat .env | grep GITHUB_TOKEN

# ビルド・起動
docker compose up -d --build

# マイグレーション実行（初回のみ）
cd src/MockOpenIdProvider
export ConnectionStrings__MockIdpDbContext="Server=localhost,1433;Database=MockIdpDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;"
export MOCK_IDP_DEFAULT_CLIENT_ID=mockclientid
export MOCK_IDP_DEFAULT_CLIENT_SECRET=mock-client-secret
export MOCK_IDP_DEFAULT_CLIENT_NAME=MockClient
export MOCK_IDP_DEFAULT_USER_EMAIL=defaultuser@example.com
export MOCK_IDP_DEFAULT_USER_PASSWORD=password
export MOCK_IDP_FEDERATE_CLIENT_ID=federateclientid
export MOCK_IDP_FEDERATE_CLIENT_SECRET=federate-client-secret
export MOCK_IDP_FEDERATE_CLIENT_NAME=FederateClient
export DEFAULT_ORGANIZATION_REDIRECT_URI=http://localhost:8080/auth/callback
export MOCK_IDP_FEDERATE_USER_EMAIL=federateuser@example.com
dotnet ef database update

# ログ確認
docker compose logs -f mockopenidprovider

# 停止
docker compose down
```

**アクセス URL:**
- HTTP: `http://localhost:9090`
- HTTPS: `https://localhost:9091`

**Docker Compose の構成:**
- `db`: SQL Server 2022 コンテナ（port 1433）
- `mockopenidprovider`: MockOpenIdProvider API（port 9090:HTTP, 9091:HTTPS）

**注意事項:**
- `.env` に `GITHUB_TOKEN` が設定されている必要があります
- Docker ビルド時に `EcAuth.IdpUtilities` パッケージをダウンロードするため
- 初回起動後にマイグレーションの実行が必要です

## プロジェクト構造

```
EcAuth.MockIdP/
├── src/
│   └── MockOpenIdProvider/           # メインアプリケーション
│       ├── Controllers/              # API コントローラー
│       │   ├── AuthorizationController.cs
│       │   ├── TokenController.cs
│       │   └── UserinfoController.cs
│       ├── Models/                   # エンティティモデル
│       │   ├── Organization.cs
│       │   ├── MockIdpUser.cs
│       │   ├── Client.cs
│       │   ├── AuthorizationCode.cs
│       │   ├── AccessToken.cs
│       │   ├── RefreshToken.cs
│       │   └── IdpDbContext.cs
│       ├── Services/                 # ビジネスロジック
│       │   ├── IOrganizationService.cs
│       │   ├── OrganizationService.cs
│       │   ├── ITokenService.cs
│       │   └── TokenService.cs
│       ├── Middlewares/              # リクエストパイプライン
│       │   └── OrganizationMiddleware.cs
│       ├── Migrations/               # EF Core マイグレーション
│       ├── Program.cs                # アプリケーションエントリポイント
│       ├── Dockerfile                # コンテナイメージ定義
│       └── MockOpenIdProvider.csproj
├── tests/
│   └── MockOpenIdProvider.Test/      # ユニットテスト
│       ├── TokenControllerTests.cs
│       └── MockOpenIdProvider.Test.csproj
├── e2e-tests/                        # Playwright E2E テスト
│   ├── tests/
│   │   ├── common/                   # 全organization共通テスト
│   │   │   └── authorization_code_flow.spec.ts
│   │   └── organizations/            # organization固有テスト
│   │       ├── dev.spec.ts
│   │       ├── staging.spec.ts
│   │       └── production.spec.ts
│   ├── fixtures/                     # カスタムfixture定義
│   │   └── organization.ts
│   ├── playwright.config.ts
│   ├── package.json
│   └── README.md
├── .github/
│   └── workflows/                    # CI/CD パイプライン
│       ├── ci.yml                    # ビルド・テスト
│       ├── docker-build.yml          # Docker イメージビルド/プッシュ
│       ├── deploy.yml                # Azure デプロイ
│       └── e2e-tests.yml             # E2E テスト
├── nuget.config                      # NuGet ソース（GitHub Packages）
├── EcAuth.MockIdP.sln                # ソリューションファイル
├── README.md
├── CLAUDE.md
├── .env.dist                         # 環境変数テンプレート
└── LICENSE
```

## 主要コンポーネント

### コントローラー

- **AuthorizationController**: `/authorization` エンドポイント処理
  - 認可コード生成
  - クライアントへコードと state を付けてリダイレクト
- **TokenController**: `/token` エンドポイント処理
  - 認可コード検証
  - アクセストークンと ID トークン発行
- **UserinfoController**: `/userinfo` エンドポイント処理
  - アクセストークンに基づくユーザー情報返却

### サービス

- **OrganizationService**: 現在の organization コンテキスト管理
  - Scoped ライフタイム（リクエストごと）
  - OrganizationMiddleware により設定
- **TokenService**: トークン生成・検証
  - 認可コード（短命）
  - アクセストークン（1時間）
  - リフレッシュトークン（30日）

### ミドルウェア

- **OrganizationMiddleware**: リクエストから organization を抽出
  - クエリパラメータ: `?org=dev`
  - HTTP ヘッダー: `X-Organization: dev`
  - デフォルト: `dev`
  - organization が見つからない場合は 400 エラー

### データベースコンテキスト

- **IdpDbContext**: グローバルクエリフィルター付き EF Core DbContext
  - 全クエリを自動的に OrganizationId でフィルタリング
  - SaveChangesAsync() オーバーライドで OrganizationId を自動設定
  - Organization エンティティはフィルターから除外

## 依存関係

### NuGet パッケージ

- **EcAuth.IdpUtilities** (1.0.0): GitHub Packages からの共通ユーティリティ
  - `EmailHashUtil`: SHA-256 メールハッシュ化
  - `Iron`: State パラメータ暗号化
  - `RandomUtil`: セキュアランダム文字列生成
- **Microsoft.EntityFrameworkCore.SqlServer** (9.0.x)
- **DotNetEnv** (3.x): 環境変数管理

### 認証設定

このプロジェクトは GitHub Packages 認証が必要です:

```bash
# GitHub CLI 使用
echo "protocol=https
host=github.com" | gh auth git-credential get | awk -F= '/username/ {u=$2} /password/ {p=$2} END {system("dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json --name github --username " u " --password " p " --store-password-in-clear-text")}'
```

## コーディング規約

- 行末の空白を削除
- 改行コードは LF
- 日本語コメント・ドキュメント可
- C# 命名規則に従う（public メンバーは PascalCase）
- セキュリティ脆弱性（SQL Injection、XSS など）を回避

## テスト

### ユニットテスト

- **TokenControllerTests.cs**: トークンエンドポイントロジックのテスト
- 分離のため In-Memory データベース使用
- サービスモック用に Moq 使用

### E2E テスト

この MockIdP は EcAuth E2E テストで使用されます:
- Azure Container Apps にデプロイ
- `https://mock-idp.azurecontainerapps.io` 経由でアクセス
- E2E テストで organization 指定: `?org=dev`

**Playwright E2E テスト**:

EcAuth.MockIdP リポジトリには、Playwright を使用した E2E テストスイートが含まれています（`e2e-tests/` ディレクトリ）。

#### セットアップ

```bash
cd e2e-tests

# 依存関係インストール
npm install

# Playwright インストール
npx playwright install --with-deps chromium

# 環境変数設定
cp .env.example .env
# .env を編集して環境を設定
```

#### テスト実行

```bash
# 全テスト実行
npm test

# Organization 別実行
npm run test:dev
npm run test:staging
npm run test:production

# デバッグモード
npm run test:debug
npm run test:ui
```

#### Organization Fixture

E2E テストは Playwright の fixture 機能を活用し、マルチ Organization（dev/staging/production）対応のテストアーキテクチャを実現しています。

```typescript
test('テスト名', async ({
  organization,    // "dev", "staging", "production"
  endpoints,       // { authorization, token, userinfo }
  clientId,        // Client ID
  testUser,        // { email, password }
}) => {
  // organization 固有のエンドポイントを使用
  await page.goto(endpoints.authorization);
});
```

詳細は @e2e-tests/README.md を参照してください。

## デプロイ

### Azure リソース

- **Azure SQL Database**: 無料プラン（32GB、月間100k vCore秒）
- **Azure Container Apps**: 自動スケーリング（0-3 インスタンス）
- **Log Analytics Workspace**: 監視用

### コスト最適化

- SQL Database: 無料プラン（¥0）
- Container Apps: 最小使用量（月額約¥75）
- アイドル時はゼロにスケール

## トラブルシューティング

### ビルドエラー

```bash
# NuGet restore 失敗時:
dotnet nuget list source
# "github" ソースが設定されていることを確認

# NuGet キャッシュクリア
dotnet nuget locals all --clear
dotnet restore --force
```

### データベース接続問題

```bash
# 接続文字列テスト
dotnet ef dbcontext info

# ファイアウォールルール確認（Azure SQL）
az sql server firewall-rule list --server <server> --resource-group <rg>
```

### Organization フィルタリング問題

- `Program.cs` で OrganizationMiddleware が登録されているか確認
- OrganizationService が Scoped として登録されているか確認
- `OnModelCreating()` でグローバルクエリフィルターが適用されているか確認

## 関連ドキュメント

- [EcAuth メインリポジトリ](https://github.com/EcAuth/EcAuth)
- [EcAuth.IdpUtilities](https://github.com/EcAuth/EcAuth.IdpUtilities)
- [ecauth-infrastructure](https://github.com/EcAuth/ecauth-infrastructure)
