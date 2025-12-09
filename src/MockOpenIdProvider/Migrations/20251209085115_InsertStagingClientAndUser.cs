using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using MockOpenIdProvider.Models;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class InsertStagingClientAndUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 環境変数から値を取得（GitHub Actions 対応）
            // .env ファイルが存在する場合は読み込む（ローカル開発環境用）
            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch
            {
                // .env ファイルが存在しない場合（GitHub Actions 等）は無視
            }

            // staging 用環境変数（MOCKIDP_STAGING_* プレフィックス）
            var MOCKIDP_STAGING_CLIENT_ID = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_CLIENT_ID")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_CLIENT_ID is required");
            var MOCKIDP_STAGING_CLIENT_SECRET = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_CLIENT_SECRET")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_CLIENT_SECRET is required");
            var MOCKIDP_STAGING_CLIENT_NAME = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_CLIENT_NAME")
                ?? "StagingClient";
            var MOCKIDP_STAGING_REDIRECT_URI = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_REDIRECT_URI")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_REDIRECT_URI is required");
            var MOCKIDP_STAGING_USER_EMAIL = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_USER_EMAIL")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_USER_EMAIL is required");
            var MOCKIDP_STAGING_USER_PASSWORD = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_USER_PASSWORD")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_USER_PASSWORD is required");

            // RSA鍵ペアを生成
            using RSA rsa = RSA.Create();
            var privateKey = rsa.ExportRSAPrivateKeyPem();
            var publicKey = rsa.ExportRSAPublicKeyPem();

            // staging organization の ID を取得してクライアントを挿入
            migrationBuilder.Sql($@"
                DECLARE @stagingOrgId INT
                SELECT @stagingOrgId = id FROM organization WHERE tenant_name = 'staging'

                INSERT INTO client (
                    client_id, client_secret, client_name, redirect_uri, public_key, private_key, organization_id
                )
                VALUES (
                    '{MOCKIDP_STAGING_CLIENT_ID}',
                    '{MOCKIDP_STAGING_CLIENT_SECRET}',
                    '{MOCKIDP_STAGING_CLIENT_NAME}',
                    '{MOCKIDP_STAGING_REDIRECT_URI}',
                    '{publicKey}',
                    '{privateKey}',
                    @stagingOrgId
                )
            ");

            // パスワードをハッシュ化
            PasswordHasher<MockIdpUser> passwordHasher = new PasswordHasher<MockIdpUser>();
            var tempUser = new MockIdpUser { Email = MOCKIDP_STAGING_USER_EMAIL };
            var hashedPassword = passwordHasher.HashPassword(tempUser, MOCKIDP_STAGING_USER_PASSWORD);

            // staging organization の ID を取得してユーザーを挿入
            migrationBuilder.Sql($@"
                DECLARE @stagingOrgId INT
                SELECT @stagingOrgId = id FROM organization WHERE tenant_name = 'staging'

                INSERT INTO mock_idp_user (
                    email, password, created_at, updated_at, ClientId, organization_id
                )
                SELECT
                    '{MOCKIDP_STAGING_USER_EMAIL}',
                    '{hashedPassword}',
                    SYSDATETIMEOFFSET(),
                    SYSDATETIMEOFFSET(),
                    c.id,
                    @stagingOrgId
                FROM client c
                WHERE c.client_id = '{MOCKIDP_STAGING_CLIENT_ID}'
                  AND c.organization_id = @stagingOrgId
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 環境変数から値を取得（GitHub Actions 対応）
            // .env ファイルが存在する場合は読み込む（ローカル開発環境用）
            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch
            {
                // .env ファイルが存在しない場合（GitHub Actions 等）は無視
            }

            var MOCKIDP_STAGING_USER_EMAIL = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_USER_EMAIL")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_USER_EMAIL is required");
            var MOCKIDP_STAGING_CLIENT_ID = Environment.GetEnvironmentVariable("MOCKIDP_STAGING_CLIENT_ID")
                ?? throw new InvalidOperationException("MOCKIDP_STAGING_CLIENT_ID is required");

            migrationBuilder.Sql($@"
                DECLARE @stagingOrgId INT
                SELECT @stagingOrgId = id FROM organization WHERE tenant_name = 'staging'

                DELETE FROM mock_idp_user
                WHERE email = '{MOCKIDP_STAGING_USER_EMAIL}'
                  AND organization_id = @stagingOrgId
            ");

            migrationBuilder.Sql($@"
                DECLARE @stagingOrgId INT
                SELECT @stagingOrgId = id FROM organization WHERE tenant_name = 'staging'

                DELETE FROM client
                WHERE client_id = '{MOCKIDP_STAGING_CLIENT_ID}'
                  AND organization_id = @stagingOrgId
            ");
        }
    }
}
