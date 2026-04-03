using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <summary>
    /// EcAuth API バージョニング（/v1 プレフィックス）導入に伴い、
    /// 全クライアントの redirect_uri を環境変数 DEFAULT_ORGANIZATION_REDIRECT_URI から更新する。
    ///
    /// EnvironmentClientUserSeeder は MOCKIDP_DEV_CLIENT_ID に対応するクライアントのみ更新するため、
    /// Federate クライアント等の redirect_uri はこのマイグレーションで一括更新する。
    ///
    /// Refs: EcAuth/EcAuth#348
    /// </summary>
    public partial class UpdateRedirectUriV1Prefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch
            {
                // .env ファイルが存在しない場合（GitHub Actions 等）は無視
            }

            var redirectUri = Environment.GetEnvironmentVariable("DEFAULT_ORGANIZATION_REDIRECT_URI")
                ?? "https://localhost:8081/v1/auth/callback";

            migrationBuilder.Sql($@"
                UPDATE client
                SET redirect_uri = '{redirectUri}'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE client
                SET redirect_uri = 'https://localhost:8081/auth/callback'
            ");
        }
    }
}
