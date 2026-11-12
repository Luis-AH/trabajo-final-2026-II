using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SegundaOportunidad.Data.Migrations
{
    /// <inheritdoc />
    public partial class UbicacionesPeruEncuentros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncuentroConfirmadoPor",
                table: "Intercambios",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EncuentroConfirmadoUtc",
                table: "Intercambios",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EncuentroLatitud",
                table: "Intercambios",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EncuentroLongitud",
                table: "Intercambios",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncuentroNombre",
                table: "Intercambios",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncuentroPropuestoPor",
                table: "Intercambios",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EncuentroPropuestoUtc",
                table: "Intercambios",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncuentroVersion",
                table: "Intercambios",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("UPDATE Intercambios SET EncuentroVersion=lower(hex(randomblob(16))) WHERE length(EncuentroVersion)=0;");

            migrationBuilder.AddColumn<string>(
                name: "Departamento",
                table: "Articulos",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Distrito",
                table: "Articulos",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LatitudPublica",
                table: "Articulos",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudPublica",
                table: "Articulos",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provincia",
                table: "Articulos",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UbicacionPlaceId",
                table: "Articulos",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UbicacionValidadaUtc",
                table: "Articulos",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncuentroConfirmadoPor",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroConfirmadoUtc",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroLatitud",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroLongitud",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroNombre",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroPropuestoPor",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroPropuestoUtc",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "EncuentroVersion",
                table: "Intercambios");

            migrationBuilder.DropColumn(
                name: "Departamento",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "Distrito",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "LatitudPublica",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "LongitudPublica",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "Provincia",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "UbicacionPlaceId",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "UbicacionValidadaUtc",
                table: "Articulos");
        }
    }
}
