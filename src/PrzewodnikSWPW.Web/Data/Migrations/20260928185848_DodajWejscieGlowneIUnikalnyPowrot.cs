using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrzewodnikSWPW.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DodajWejscieGlowneIUnikalnyPowrot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Kierunek_KierunekPowrotnyId",
                table: "Kierunek");

            migrationBuilder.AddColumn<int>(
                name: "PunktWejsciaGlownegoId",
                table: "Budynek",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Kierunek_Powrotny",
                table: "Kierunek",
                column: "KierunekPowrotnyId",
                unique: true,
                filter: "[KierunekPowrotnyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Budynek_PunktWejsciaGlownegoId",
                table: "Budynek",
                column: "PunktWejsciaGlownegoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Budynek_PunktRuchu_PunktWejsciaGlownegoId",
                table: "Budynek",
                column: "PunktWejsciaGlownegoId",
                principalTable: "PunktRuchu",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Budynek_PunktRuchu_PunktWejsciaGlownegoId",
                table: "Budynek");

            migrationBuilder.DropIndex(
                name: "UQ_Kierunek_Powrotny",
                table: "Kierunek");

            migrationBuilder.DropIndex(
                name: "IX_Budynek_PunktWejsciaGlownegoId",
                table: "Budynek");

            migrationBuilder.DropColumn(
                name: "PunktWejsciaGlownegoId",
                table: "Budynek");

            migrationBuilder.CreateIndex(
                name: "IX_Kierunek_KierunekPowrotnyId",
                table: "Kierunek",
                column: "KierunekPowrotnyId");
        }
    }
}
