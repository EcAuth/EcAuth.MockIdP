using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using MockOpenIdProvider.Models;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class InsertFederateClient : Migration
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

            var MOCK_IDP_FEDERATE_CLIENT_ID = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_CLIENT_ID")
                ?? throw new InvalidOperationException("MOCK_IDP_FEDERATE_CLIENT_ID is required");
            var MOCK_IDP_FEDERATE_CLIENT_SECRET = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_CLIENT_SECRET")
                ?? throw new InvalidOperationException("MOCK_IDP_FEDERATE_CLIENT_SECRET is required");
            var MOCK_IDP_FEDERATE_CLIENT_NAME = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_CLIENT_NAME")
                ?? throw new InvalidOperationException("MOCK_IDP_FEDERATE_CLIENT_NAME is required");
            var DEFAULT_ORGANIZATION_REDIRECT_URI = Environment.GetEnvironmentVariable("DEFAULT_ORGANIZATION_REDIRECT_URI")
                ?? throw new InvalidOperationException("DEFAULT_ORGANIZATION_REDIRECT_URI is required");
            var MOCK_IDP_FEDERATE_USER_EMAIL = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_USER_EMAIL")
                ?? throw new InvalidOperationException("MOCK_IDP_FEDERATE_USER_EMAIL is required");
            var MOCK_IDP_DEFAULT_USER_PASSWORD = Environment.GetEnvironmentVariable("MOCK_IDP_DEFAULT_USER_PASSWORD")
                ?? throw new InvalidOperationException("MOCK_IDP_DEFAULT_USER_PASSWORD is required");

            // RSA鍵ペアを生成
            using RSA rsa = RSA.Create();
            var privateKey = rsa.ExportRSAPrivateKeyPem();
            var publicKey = rsa.ExportRSAPublicKeyPem();

            // クライアントの挿入
            migrationBuilder.Sql($@"
                INSERT INTO client (
                    client_id, client_secret, client_name, redirect_uri, public_key, private_key
                )
                VALUES (
                    '{MOCK_IDP_FEDERATE_CLIENT_ID}',
                    '{MOCK_IDP_FEDERATE_CLIENT_SECRET}',
                    '{MOCK_IDP_FEDERATE_CLIENT_NAME}',
                    '{DEFAULT_ORGANIZATION_REDIRECT_URI}',
                    '{publicKey}',
                    '{privateKey}'
                )
            ");

            // パスワードをハッシュ化
            PasswordHasher<MockIdpUser> passwordHasher = new PasswordHasher<MockIdpUser>();
            var tempUser = new MockIdpUser { Email = MOCK_IDP_FEDERATE_USER_EMAIL };
            var hashedPassword = passwordHasher.HashPassword(tempUser, MOCK_IDP_DEFAULT_USER_PASSWORD);

            // ユーザーの挿入
            migrationBuilder.Sql($@"
                INSERT INTO mock_idp_user (
                    email, password, created_at, updated_at, ClientId
                )
                SELECT 
                    '{MOCK_IDP_FEDERATE_USER_EMAIL}',
                    '{hashedPassword}',
                    SYSDATETIMEOFFSET(),
                    SYSDATETIMEOFFSET(),
                    c.id
                FROM client c
                WHERE c.client_id = '{MOCK_IDP_FEDERATE_CLIENT_ID}'
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

            var MOCK_IDP_FEDERATE_USER_EMAIL = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_USER_EMAIL")
                ?? throw new InvalidOperationException("MOCK_IDP_FEDERATE_USER_EMAIL is required");
            var MOCK_IDP_FEDERATE_CLIENT_ID = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_CLIENT_ID")
                ?? throw new InvalidOperationException("MOCK_IDP_FEDERATE_CLIENT_ID is required");

            migrationBuilder.Sql($@"
                DELETE FROM mock_idp_user 
                WHERE email = '{MOCK_IDP_FEDERATE_USER_EMAIL}'
            ");

            migrationBuilder.Sql($@"
                DELETE FROM client 
                WHERE client_id = '{MOCK_IDP_FEDERATE_CLIENT_ID}'
            ");
        }
    }
}