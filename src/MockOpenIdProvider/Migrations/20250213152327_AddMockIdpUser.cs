using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using MockOpenIdProvider.Models;

#nullable disable

namespace MockOpenIdProvider.Migrations
{
    /// <inheritdoc />
    public partial class AddMockIdpUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM authorization_code");
            migrationBuilder.Sql("DELETE FROM access_token");
            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "authorization_code",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "access_token",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "mock_idp_user",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_idp_user", x => x.id);
                    table.ForeignKey(
                        name: "FK_mock_idp_user_client_ClientId",
                        column: x => x.ClientId,
                        principalTable: "client",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_authorization_code_user_id",
                table: "authorization_code",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_access_token_user_id",
                table: "access_token",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_idp_user_ClientId",
                table: "mock_idp_user",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_access_token_mock_idp_user_user_id",
                table: "access_token",
                column: "user_id",
                principalTable: "mock_idp_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_authorization_code_mock_idp_user_user_id",
                table: "authorization_code",
                column: "user_id",
                principalTable: "mock_idp_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_access_token_mock_idp_user_user_id",
                table: "access_token");

            migrationBuilder.DropForeignKey(
                name: "FK_authorization_code_mock_idp_user_user_id",
                table: "authorization_code");

            migrationBuilder.DropTable(
                name: "mock_idp_user");

            migrationBuilder.DropIndex(
                name: "IX_authorization_code_user_id",
                table: "authorization_code");

            migrationBuilder.DropIndex(
                name: "IX_access_token_user_id",
                table: "access_token");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "authorization_code");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "access_token");
        }
    }
}
