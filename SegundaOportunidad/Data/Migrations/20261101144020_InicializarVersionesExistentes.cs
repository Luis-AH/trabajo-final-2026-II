using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SegundaOportunidad.Data.Migrations
{
    /// <inheritdoc />
    public partial class InicializarVersionesExistentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Articulos SET ModeracionVersion = lower(hex(randomblob(16))) WHERE trim(ModeracionVersion) = '';");
            migrationBuilder.Sql("UPDATE AspNetUsers SET SanctionVersion = lower(hex(randomblob(16))) WHERE trim(SanctionVersion) = '';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
