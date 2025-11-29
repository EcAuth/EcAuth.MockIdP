using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClientRedirectUri : Migration
    {
        // E2E テストで使用する redirect_uri
        private const string NEW_REDIRECT_URI = "https://localhost:8081/auth/callback";
        // 以前の redirect_uri（ロールバック用）
        private const string OLD_REDIRECT_URI = "http://localhost:8080/auth/callback";

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

            var MOCK_IDP_DEFAULT_CLIENT_ID = Environment.GetEnvironmentVariable("MOCK_IDP_DEFAULT_CLIENT_ID")
                ?? "mockclientid";
            var MOCK_IDP_FEDERATE_CLIENT_ID = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_CLIENT_ID")
                ?? "federateclientid";

            // mockclientid の redirect_uri を更新
            migrationBuilder.Sql($@"
                UPDATE client
                SET redirect_uri = '{NEW_REDIRECT_URI}'
                WHERE client_id = '{MOCK_IDP_DEFAULT_CLIENT_ID}'
            ");

            // federateclientid の redirect_uri を更新
            migrationBuilder.Sql($@"
                UPDATE client
                SET redirect_uri = '{NEW_REDIRECT_URI}'
                WHERE client_id = '{MOCK_IDP_FEDERATE_CLIENT_ID}'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 環境変数から値を取得（GitHub Actions 対応）
            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch
            {
                // .env ファイルが存在しない場合は無視
            }

            var MOCK_IDP_DEFAULT_CLIENT_ID = Environment.GetEnvironmentVariable("MOCK_IDP_DEFAULT_CLIENT_ID")
                ?? "mockclientid";
            var MOCK_IDP_FEDERATE_CLIENT_ID = Environment.GetEnvironmentVariable("MOCK_IDP_FEDERATE_CLIENT_ID")
                ?? "federateclientid";

            // mockclientid の redirect_uri を元に戻す
            migrationBuilder.Sql($@"
                UPDATE client
                SET redirect_uri = '{OLD_REDIRECT_URI}'
                WHERE client_id = '{MOCK_IDP_DEFAULT_CLIENT_ID}'
            ");

            // federateclientid の redirect_uri を元に戻す
            migrationBuilder.Sql($@"
                UPDATE client
                SET redirect_uri = '{OLD_REDIRECT_URI}'
                WHERE client_id = '{MOCK_IDP_FEDERATE_CLIENT_ID}'
            ");
        }
    }
}
