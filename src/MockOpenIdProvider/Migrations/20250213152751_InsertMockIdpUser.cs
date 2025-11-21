using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using MockOpenIdProvider.Models;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class InsertMockIdpUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 環境変数から値を取得
            DotNetEnv.Env.TraversePath().Load();
            var MOCK_IDP_DEFAULT_CLIENT_ID = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_CLIENT_ID");
            var MOCK_IDP_DEFAULT_USER_EMAIL = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_USER_EMAIL");
            var MOCK_IDP_DEFAULT_USER_PASSWORD = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_USER_PASSWORD");

            // パスワードをハッシュ化
            PasswordHasher<MockIdpUser> passwordHasher = new PasswordHasher<MockIdpUser>();
            var tempUser = new MockIdpUser { Email = MOCK_IDP_DEFAULT_USER_EMAIL };
            var hashedPassword = passwordHasher.HashPassword(tempUser, MOCK_IDP_DEFAULT_USER_PASSWORD);

            // ユーザーの挿入
            migrationBuilder.Sql($@"
                INSERT INTO mock_idp_user (
                    email, password, created_at, updated_at, ClientId
                )
                SELECT 
                    '{MOCK_IDP_DEFAULT_USER_EMAIL}',
                    '{hashedPassword}',
                    SYSDATETIMEOFFSET(),
                    SYSDATETIMEOFFSET(),
                    c.id
                FROM client c
                WHERE c.client_id = '{MOCK_IDP_DEFAULT_CLIENT_ID}'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM mock_idp_user");
        }
    }
}