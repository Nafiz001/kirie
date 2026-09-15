using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kirie.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cranes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CityId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Colour = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FoldedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cranes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    CityId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FetchedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Code = table.Column<int>(type: "INTEGER", nullable: false),
                    TemperatureC = table.Column<double>(type: "REAL", nullable: false),
                    Humidity = table.Column<double>(type: "REAL", nullable: false),
                    CloudCover = table.Column<double>(type: "REAL", nullable: false),
                    PrecipitationMm = table.Column<double>(type: "REAL", nullable: false),
                    SnowfallCm = table.Column<double>(type: "REAL", nullable: false),
                    WindKph = table.Column<double>(type: "REAL", nullable: false),
                    WindDirection = table.Column<double>(type: "REAL", nullable: false),
                    GustKph = table.Column<double>(type: "REAL", nullable: false),
                    VisibilityM = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.CityId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cranes_CityId_FoldedAt",
                table: "Cranes",
                columns: new[] { "CityId", "FoldedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cranes");

            migrationBuilder.DropTable(
                name: "Readings");
        }
    }
}
