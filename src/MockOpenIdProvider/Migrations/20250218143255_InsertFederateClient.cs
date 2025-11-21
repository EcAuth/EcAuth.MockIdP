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
            // 環境変数から値を取得
            DotNetEnv.Env.TraversePath().Load();
            var MOCK_IDP_FEDERATE_CLIENT_ID = DotNetEnv.Env.GetString("MOCK_IDP_FEDERATE_CLIENT_ID");
            var MOCK_IDP_FEDERATE_CLIENT_SECRET = DotNetEnv.Env.GetString("MOCK_IDP_FEDERATE_CLIENT_SECRET");
            var MOCK_IDP_FEDERATE_CLIENT_NAME = DotNetEnv.Env.GetString("MOCK_IDP_FEDERATE_CLIENT_NAME");
            var DEFAULT_ORGANIZATION_REDIRECT_URI = DotNetEnv.Env.GetString("DEFAULT_ORGANIZATION_REDIRECT_URI");
            var MOCK_IDP_FEDERATE_USER_EMAIL = DotNetEnv.Env.GetString("MOCK_IDP_FEDERATE_USER_EMAIL");
            var MOCK_IDP_DEFAULT_USER_PASSWORD = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_USER_PASSWORD");

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
            // 環境変数から値を取得
            DotNetEnv.Env.TraversePath().Load();
            var MOCK_IDP_FEDERATE_USER_EMAIL = DotNetEnv.Env.GetString("MOCK_IDP_FEDERATE_USER_EMAIL");
            var MOCK_IDP_FEDERATE_CLIENT_ID = DotNetEnv.Env.GetString("MOCK_IDP_FEDERATE_CLIENT_ID");

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