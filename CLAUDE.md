# CLAUDE.md

このファイルは、Claude Code が EcAuth.MockIdP コードベースを操作する際のガイダンスを提供します。

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

```bash
# Docker イメージビルド（リポジトリルートから）
docker build --build-arg GITHUB_TOKEN=<your_token> -t mock-idp:latest -f src/MockOpenIdProvider/Dockerfile .

# コンテナ起動
docker run -d -p 8080:8080 -p 8081:8081 \
  -e ConnectionStrings__MockIdpDbContext="Server=..." \
  mock-idp:latest
```

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
├── .github/
│   └── workflows/                    # CI/CD パイプライン
│       ├── ci.yml                    # ビルド・テスト
│       ├── docker-build.yml          # Docker イメージビルド/プッシュ
│       └── deploy.yml                # Azure デプロイ
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
