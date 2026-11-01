using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SegundaOportunidad.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminGuardAnalisis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalisisAdminGuard",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AdministradorId = table.Column<string>(type: "TEXT", nullable: false),
                    ArticuloId = table.Column<string>(type: "TEXT", nullable: true),
                    ReporteId = table.Column<string>(type: "TEXT", nullable: true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Consulta = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Modelo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ImagenSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Resultado = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Completado = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisisAdminGuard", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalisisAdminGuard_AspNetUsers_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisisAdminGuard_AdministradorId_CreadoUtc",
                table: "AnalisisAdminGuard",
                columns: new[] { "AdministradorId", "CreadoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisisAdminGuard_ImagenSha256",
                table: "AnalisisAdminGuard",
                column: "ImagenSha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisisAdminGuard");
        }
    }
}
