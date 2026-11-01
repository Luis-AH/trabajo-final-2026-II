using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SegundaOportunidad.Data.Migrations
{
    /// <inheritdoc />
    public partial class AprobacionUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedByAdminId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE AspNetUsers SET IsVerified = 0, Dni = NULL WHERE IsVerified = 1 AND VerifiedByAdminId IS NULL AND Id NOT IN (SELECT ur.UserId FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = 'Admin');");
            migrationBuilder.CreateTable(
                name: "EstadosSistema",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosSistema", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_VerifiedByAdminId",
                table: "AspNetUsers",
                column: "VerifiedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_VerifiedByAdminId",
                table: "AspNetUsers",
                column: "VerifiedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_VerifiedByAdminId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "EstadosSistema");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_VerifiedByAdminId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerifiedByAdminId",
                table: "AspNetUsers");
        }
    }
}
