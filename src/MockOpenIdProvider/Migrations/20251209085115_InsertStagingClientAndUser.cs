using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class InsertStagingClientAndUser : Migration
    {
        // シードデータは DbInitializer (EnvironmentClientUserSeeder) に移行済み。
        // このマイグレーションは __EFMigrationsHistory に記録済みのため、
        // ファイル自体は削除できません。

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
