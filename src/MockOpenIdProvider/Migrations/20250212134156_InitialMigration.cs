using System;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    client_id = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    client_secret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    client_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    redirect_uri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    public_key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    private_key = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "access_token",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    expires_in = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    client_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_token", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_token_client_client_id",
                        column: x => x.client_id,
                        principalTable: "client",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "authorization_code",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    expires_in = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    used = table.Column<bool>(type: "bit", nullable: false),
                    client_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_code", x => x.id);
                    table.ForeignKey(
                        name: "FK_authorization_code_client_client_id",
                        column: x => x.client_id,
                        principalTable: "client",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_token_client_id",
                table: "access_token",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_code_client_id",
                table: "authorization_code",
                column: "client_id");

            using RSA rsa = RSA.Create();
            var privateKey = rsa.ExportRSAPrivateKeyPem();
            var publicKey = rsa.ExportRSAPublicKeyPem();

            var CLIENT_ID = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_CLIENT_ID");
            var CLIENT_SECRET = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_CLIENT_SECRET");
            var CLIENT_NAME = DotNetEnv.Env.GetString("MOCK_IDP_DEFAULT_CLIENT_NAME");
            var DEFAULT_ORGANIZATION_REDIRECT_URI = DotNetEnv.Env.GetString("DEFAULT_ORGANIZATION_REDIRECT_URI");
            migrationBuilder.Sql(@$"
                INSERT INTO client
                    (client_id, client_secret, client_name, redirect_uri, public_key, private_key)
                VALUES
                    ('{CLIENT_ID}', '{CLIENT_SECRET}', '{CLIENT_NAME}', '{DEFAULT_ORGANIZATION_REDIRECT_URI}', '{publicKey}', '{privateKey}');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_token");

            migrationBuilder.DropTable(
                name: "authorization_code");

            migrationBuilder.DropTable(
                name: "client");
            migrationBuilder.Sql(@"
                DELETE FROM client WHERE client_id = 'mockclientid';
            ");
        }
    }
}
