using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SegundaOportunidad.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReportesModeracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reportes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DenuncianteId = table.Column<string>(type: "TEXT", nullable: false),
                    DenunciadoId = table.Column<string>(type: "TEXT", nullable: false),
                    ArticuloId = table.Column<string>(type: "TEXT", nullable: true),
                    Motivo = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EvidenciaUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    ClaveAbierta = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reportes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reportes_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reportes_AspNetUsers_DenunciadoId",
                        column: x => x.DenunciadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reportes_AspNetUsers_DenuncianteId",
                        column: x => x.DenuncianteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecisionesReportes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReporteId = table.Column<string>(type: "TEXT", nullable: false),
                    AdministradorId = table.Column<string>(type: "TEXT", nullable: false),
                    Anterior = table.Column<int>(type: "INTEGER", nullable: false),
                    Nuevo = table.Column<int>(type: "INTEGER", nullable: false),
                    Justificacion = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionesReportes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionesReportes_AspNetUsers_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DecisionesReportes_Reportes_ReporteId",
                        column: x => x.ReporteId,
                        principalTable: "Reportes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesReportes_AdministradorId",
                table: "DecisionesReportes",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesReportes_ReporteId",
                table: "DecisionesReportes",
                column: "ReporteId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_ArticuloId",
                table: "Reportes",
                column: "ArticuloId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_ClaveAbierta",
                table: "Reportes",
                column: "ClaveAbierta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_DenunciadoId",
                table: "Reportes",
                column: "DenunciadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_DenuncianteId",
                table: "Reportes",
                column: "DenuncianteId");

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_Estado_CreadoUtc",
                table: "Reportes",
                columns: new[] { "Estado", "CreadoUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionesReportes");

            migrationBuilder.DropTable(
                name: "Reportes");
        }
    }
}
