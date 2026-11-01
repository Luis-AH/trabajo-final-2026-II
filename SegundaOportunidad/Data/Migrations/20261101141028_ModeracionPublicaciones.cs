using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SegundaOportunidad.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModeracionPublicaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Moderacion",
                table: "Articulos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModeracionVersion",
                table: "Articulos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ModeracionesPublicaciones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticuloId = table.Column<string>(type: "TEXT", nullable: false),
                    AdministradorId = table.Column<string>(type: "TEXT", nullable: false),
                    Anterior = table.Column<int>(type: "INTEGER", nullable: false),
                    Nuevo = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeracionesPublicaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModeracionesPublicaciones_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModeracionesPublicaciones_AspNetUsers_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModeracionesPublicaciones_AdministradorId",
                table: "ModeracionesPublicaciones",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeracionesPublicaciones_ArticuloId_FechaUtc",
                table: "ModeracionesPublicaciones",
                columns: new[] { "ArticuloId", "FechaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModeracionesPublicaciones");

            migrationBuilder.DropColumn(
                name: "Moderacion",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "ModeracionVersion",
                table: "Articulos");
        }
    }
}
