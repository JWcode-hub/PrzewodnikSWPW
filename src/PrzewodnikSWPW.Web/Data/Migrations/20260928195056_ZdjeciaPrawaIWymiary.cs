using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrzewodnikSWPW.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ZdjeciaPrawaIWymiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bez wartości domyślnej: zdjęcie bez źródła i licencji (D-09) ma zatrzymać migrację,
            // a nie dostać pusty napis, który i tak odrzuci CK_Zdjecie_Prawa.
            migrationBuilder.AlterColumn<string>(
                name: "Zrodlo",
                table: "Zdjecie",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Licencja",
                table: "Zdjecie",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Szerokosc",
                table: "Zdjecie",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Wysokosc",
                table: "Zdjecie",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zdjecie_Prawa",
                table: "Zdjecie",
                sql: "LEN(LTRIM(RTRIM([Zrodlo]))) > 0 AND LEN(LTRIM(RTRIM([Licencja]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Zdjecie_Wymiary",
                table: "Zdjecie",
                sql: "([Szerokosc] IS NULL AND [Wysokosc] IS NULL) OR ([Szerokosc] > 0 AND [Wysokosc] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Zdjecie_Prawa",
                table: "Zdjecie");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Zdjecie_Wymiary",
                table: "Zdjecie");

            migrationBuilder.DropColumn(
                name: "Szerokosc",
                table: "Zdjecie");

            migrationBuilder.DropColumn(
                name: "Wysokosc",
                table: "Zdjecie");

            migrationBuilder.AlterColumn<string>(
                name: "Zrodlo",
                table: "Zdjecie",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Licencja",
                table: "Zdjecie",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
