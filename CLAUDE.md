# CLAUDE.md

このファイルは、Claude Code が EcAuth.MockIdP コードベースを操作する際のガイダンスを提供します。
日本語で回答してください。

## プロジェクト概要

EcAuth.MockIdP は、EcAuth Identity Provider の E2E テスト用に設計された Mock OpenID Connect Provider です。制御された環境で外部 IdP（Google、LINE、Facebook など）をシミュレートします。

**目的**:
- EcAuth IdentityProvider の E2E テスト
- 開発・ステージング環境でのテスト
- 本番データからの分離
- コスト最適化された Azure デプロイ（月額約¥800）

## 関連リポジトリ

| リポジトリ                                                               | 説明                                    |
|--------------------------------------------------------------------------|-----------------------------------------|
| [EcAuth](https://github.com/EcAuth/EcAuth)                               | IdentityProvider メインアプリケーション |
| [EcAuth.IdpUtilities](https://github.com/EcAuth/EcAuth.IdpUtilities)     | 共通ユーティリティライブラリ            |
| [ecauth-infrastructure](https://github.com/EcAuth/ecauth-infrastructure) | IaC（Terraform + Ansible）              |
| [EcAuthDocs](https://github.com/EcAuth/EcAuthDocs)                       | 設計ドキュメント                        |

## 開発コマンド

### セットアップ

```bash
# リポジトリクローン
git clone https://github.com/EcAuth/EcAuth.MockIdP.git
cd EcAuth.MockIdP

# 環境設定
cp .env.dist .env
# .env を編集してデータベース接続文字列を設定
```

### GitHub Packages 認証

このプロジェクトは GitHub Packages から `EcAuth.IdpUtilities` NuGet パッケージを取得するため、認証設定が必要です。

```bash
# GitHub CLI を使用して自動設定（一度だけ実行）
echo "protocol=https
host=github.com" | gh auth git-credential get | awk -F= '/username/ {u=$2} /password/ {p=$2} END {system("dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json --name github --username " u " --password " p " --store-password-in-clear-text")}'
```

**GitHub Actions での認証設定については [EcAuthDocs/claude-repository-guide.md](https://github.com/EcAuth/EcAuthDocs/blob/main/claude-repository-guide.md#github-packages-認証) を参照してください。**

### ビルドとテスト

```bash
# ソリューションビルド
dotnet build EcAuth.MockIdP.sln

# ユニットテスト実行
dotnet test

# 特定のテストクラス実行
dotnet test --filter ClassName=TokenControllerTests
```

### データベース操作

```bash
cd src/MockOpenIdProvider

# マイグレーション追加
dotnet ef migrations add <MigrationName>

# マイグレーション適用
export $(cat ../../.env | grep -v '^#' | xargs)
dotnet ef database update
```

### Docker Compose での起動

```bash
# .env ファイルを確認（GITHUB_TOKEN が設定されていること）
cat .env | grep GITHUB_TOKEN

# ビルド・起動
docker compose up -d --build

# ログ確認
docker compose logs -f mockopenidprovider
```

**アクセス URL:**
- HTTP: `http://localhost:9090`
- HTTPS: `https://localhost:9091`

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

### OAuth2/OIDC フロー

```
/authorization?org=dev → 認可コード生成 → クライアントにリダイレクト
/token?org=dev → 認可コード検証 → アクセストークン + ID トークン発行
/userinfo?org=dev → アクセストークン検証 → ユーザー情報返却
```

## プロジェクト構造

```
EcAuth.MockIdP/
├── src/MockOpenIdProvider/        # メインアプリケーション
│   ├── Controllers/               # API コントローラー
│   ├── Models/                    # エンティティモデル
│   ├── Services/                  # ビジネスロジック
│   ├── Middlewares/               # リクエストパイプライン
│   └── Migrations/                # EF Core マイグレーション
├── tests/MockOpenIdProvider.Test/ # ユニットテスト
├── e2e-tests/                     # Playwright E2E テスト
└── .github/workflows/             # CI/CD パイプライン
```

## E2E テスト

Playwright を使用した E2E テストスイートが `e2e-tests/` ディレクトリに配置されています。

```bash
cd e2e-tests

# 依存関係インストール
npm install

# Playwright インストール
npx playwright install --with-deps chromium

# 全テスト実行
npm test

# Organization 別実行
npm run test:dev
npm run test:staging
```

詳細は [e2e-tests/README.md](./e2e-tests/README.md) を参照してください。

## デプロイ

### Azure リソース

- **Azure SQL Database**: Basic 5DTU（2GB）
- **Azure Container Apps**: 自動スケーリング（0-3 インスタンス）
- **Log Analytics Workspace**: 監視用

### コスト最適化

- SQL Database: Basic 5DTU（月額約¥700）
- Container Apps: 最小使用量（月額約¥100）
- アイドル時はゼロにスケール

## コーディング規約

- 行末の空白を削除
- 改行コードは LF
- 日本語コメント・ドキュメント可
- セキュリティ脆弱性（SQL Injection、XSS など）を回避

## トラブルシューティング

### ビルドエラー

```bash
# NuGet restore 失敗時
dotnet nuget list source
# "github" ソースが設定されていることを確認

# NuGet キャッシュクリア
dotnet nuget locals all --clear
dotnet restore --force
```

### Organization フィルタリング問題

- `Program.cs` で OrganizationMiddleware が登録されているか確認
- OrganizationService が Scoped として登録されているか確認
- `OnModelCreating()` でグローバルクエリフィルターが適用されているか確認
