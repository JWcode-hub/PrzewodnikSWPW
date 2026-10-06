using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PrzewodnikSWPW.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Budynek",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nazwa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Adres = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Opis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpisDostepnosciArchitektonicznej = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CzyMaWinde = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CzyAktywny = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budynek", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypPunktu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nazwa = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Opis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypPunktu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypSali",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nazwa = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Opis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypSali", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Udogodnienie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nazwa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Opis = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Ikona = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Udogodnienie", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WpisAudytu",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UzytkownikId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Encja = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KluczEncji = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operacja = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WartosciStare = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WartosciNowe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataOperacji = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WpisAudytu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZgloszenieDostepnosci",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImieNazwisko = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Tresc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdresStrony = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    ObslugujeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Odpowiedz = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataZgloszenia = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    DataOdpowiedzi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZgloszenieDostepnosci", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pietro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BudynekId = table.Column<int>(type: "int", nullable: false),
                    Numer = table.Column<int>(type: "int", nullable: false),
                    Nazwa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Opis = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pietro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pietro_Budynek_BudynekId",
                        column: x => x.BudynekId,
                        principalTable: "Budynek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PunktRuchu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PietroId = table.Column<int>(type: "int", nullable: false),
                    TypPunktuId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nazwa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Opis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpisGlosowy = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    AzymutDomyslny = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    X = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Y = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    CzyAktywny = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunktRuchu", x => x.Id);
                    table.CheckConstraint("CK_PunktRuchu_Azymut", "[AzymutDomyslny] IN (0, 90, 180, 270)");
                    table.ForeignKey(
                        name: "FK_PunktRuchu_Pietro_PietroId",
                        column: x => x.PietroId,
                        principalTable: "Pietro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PunktRuchu_TypPunktu_TypPunktuId",
                        column: x => x.TypPunktuId,
                        principalTable: "TypPunktu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PunktUdogodnienie",
                columns: table => new
                {
                    PunktRuchuId = table.Column<int>(type: "int", nullable: false),
                    UdogodnienieId = table.Column<int>(type: "int", nullable: false),
                    Uwagi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunktUdogodnienie", x => new { x.PunktRuchuId, x.UdogodnienieId });
                    table.ForeignKey(
                        name: "FK_PunktUdogodnienie_PunktRuchu_PunktRuchuId",
                        column: x => x.PunktRuchuId,
                        principalTable: "PunktRuchu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PunktUdogodnienie_Udogodnienie_UdogodnienieId",
                        column: x => x.UdogodnienieId,
                        principalTable: "Udogodnienie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sala",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PietroId = table.Column<int>(type: "int", nullable: false),
                    TypSaliId = table.Column<int>(type: "int", nullable: false),
                    PunktWejsciowyId = table.Column<int>(type: "int", nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nazwa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aliasy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Opis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpisGlosowy = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    LiczbaMiejsc = table.Column<int>(type: "int", nullable: true),
                    CzyDostepnaDlaWozkow = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CzyAktywna = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sala", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sala_Pietro_PietroId",
                        column: x => x.PietroId,
                        principalTable: "Pietro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sala_PunktRuchu_PunktWejsciowyId",
                        column: x => x.PunktWejsciowyId,
                        principalTable: "PunktRuchu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sala_TypSali_TypSaliId",
                        column: x => x.TypSaliId,
                        principalTable: "TypSali",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kierunek",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PunktZrodlowyId = table.Column<int>(type: "int", nullable: false),
                    PunktDocelowyId = table.Column<int>(type: "int", nullable: true),
                    SalaDocelowaId = table.Column<int>(type: "int", nullable: true),
                    Azymut = table.Column<int>(type: "int", nullable: false),
                    Waga = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    RodzajPrzejscia = table.Column<byte>(type: "tinyint", nullable: false),
                    OpisPrzejscia = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    CzyAktywny = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CzyDostepnyBezSchodow = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    KierunekPowrotnyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kierunek", x => x.Id);
                    table.CheckConstraint("CK_Kierunek_Azymut", "[Azymut] IN (0, 90, 180, 270)");
                    table.CheckConstraint("CK_Kierunek_BezPetli", "[PunktZrodlowyId] <> [PunktDocelowyId]");
                    table.CheckConstraint("CK_Kierunek_JedenCel", "([PunktDocelowyId] IS NOT NULL AND [SalaDocelowaId] IS NULL) OR ([PunktDocelowyId] IS NULL AND [SalaDocelowaId] IS NOT NULL)");
                    table.CheckConstraint("CK_Kierunek_Waga", "[Waga] > 0 AND [Waga] <= 200");
                    table.ForeignKey(
                        name: "FK_Kierunek_Kierunek_KierunekPowrotnyId",
                        column: x => x.KierunekPowrotnyId,
                        principalTable: "Kierunek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kierunek_PunktRuchu_PunktDocelowyId",
                        column: x => x.PunktDocelowyId,
                        principalTable: "PunktRuchu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kierunek_PunktRuchu_PunktZrodlowyId",
                        column: x => x.PunktZrodlowyId,
                        principalTable: "PunktRuchu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kierunek_Sala_SalaDocelowaId",
                        column: x => x.SalaDocelowaId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaUdogodnienie",
                columns: table => new
                {
                    SalaId = table.Column<int>(type: "int", nullable: false),
                    UdogodnienieId = table.Column<int>(type: "int", nullable: false),
                    Uwagi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaUdogodnienie", x => new { x.SalaId, x.UdogodnienieId });
                    table.ForeignKey(
                        name: "FK_SalaUdogodnienie_Sala_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalaUdogodnienie_Udogodnienie_UdogodnienieId",
                        column: x => x.UdogodnienieId,
                        principalTable: "Udogodnienie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZapytanieTrasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaZId = table.Column<int>(type: "int", nullable: true),
                    SalaDoId = table.Column<int>(type: "int", nullable: true),
                    FrazaZ = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FrazaDo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrybWindy = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CzySukces = table.Column<bool>(type: "bit", nullable: false),
                    DlugoscMetry = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    LiczbaKrokow = table.Column<int>(type: "int", nullable: true),
                    CzasMs = table.Column<int>(type: "int", nullable: true),
                    DataZapytania = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZapytanieTrasy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZapytanieTrasy_Sala_SalaDoId",
                        column: x => x.SalaDoId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZapytanieTrasy_Sala_SalaZId",
                        column: x => x.SalaZId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zdjecie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PunktRuchuId = table.Column<int>(type: "int", nullable: true),
                    SalaId = table.Column<int>(type: "int", nullable: true),
                    SciezkaPliku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TekstAlternatywny = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OpisRozszerzony = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Zrodlo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Licencja = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Kolejnosc = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CzyDekoracyjne = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zdjecie", x => x.Id);
                    table.CheckConstraint("CK_Zdjecie_Alt", "[CzyDekoracyjne] = 1 OR LEN(LTRIM(RTRIM([TekstAlternatywny]))) >= 5");
                    table.CheckConstraint("CK_Zdjecie_Wlasciciel", "([PunktRuchuId] IS NOT NULL AND [SalaId] IS NULL) OR ([PunktRuchuId] IS NULL AND [SalaId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Zdjecie_PunktRuchu_PunktRuchuId",
                        column: x => x.PunktRuchuId,
                        principalTable: "PunktRuchu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Zdjecie_Sala_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Utrudnienie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KierunekId = table.Column<int>(type: "int", nullable: true),
                    PunktRuchuId = table.Column<int>(type: "int", nullable: true),
                    Przyczyna = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ObowiazujeOd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ObowiazujeDo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UtworzylId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DataUtworzenia = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utrudnienie", x => x.Id);
                    table.CheckConstraint("CK_Utrudnienie_Cel", "([KierunekId] IS NOT NULL AND [PunktRuchuId] IS NULL) OR ([KierunekId] IS NULL AND [PunktRuchuId] IS NOT NULL)");
                    table.CheckConstraint("CK_Utrudnienie_Daty", "[ObowiazujeDo] IS NULL OR [ObowiazujeDo] > [ObowiazujeOd]");
                    table.ForeignKey(
                        name: "FK_Utrudnienie_Kierunek_KierunekId",
                        column: x => x.KierunekId,
                        principalTable: "Kierunek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Utrudnienie_PunktRuchu_PunktRuchuId",
                        column: x => x.PunktRuchuId,
                        principalTable: "PunktRuchu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObszarAktywny",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZdjecieId = table.Column<int>(type: "int", nullable: false),
                    KierunekId = table.Column<int>(type: "int", nullable: false),
                    Ksztalt = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Wspolrzedne = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Etykieta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObszarAktywny", x => x.Id);
                    table.CheckConstraint("CK_Obszar_Ksztalt", "[Ksztalt] IN ('rect', 'circle', 'poly')");
                    table.ForeignKey(
                        name: "FK_ObszarAktywny_Kierunek_KierunekId",
                        column: x => x.KierunekId,
                        principalTable: "Kierunek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ObszarAktywny_Zdjecie_ZdjecieId",
                        column: x => x.ZdjecieId,
                        principalTable: "Zdjecie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TypPunktu",
                columns: new[] { "Id", "Nazwa", "Opis" },
                values: new object[,]
                {
                    { 1, "Hol", "Otwarta przestrzeń wejściowa" },
                    { 2, "Korytarz", "Odcinek korytarza" },
                    { 3, "Skrzyżowanie", "Miejsce, w którym korytarze się krzyżują" },
                    { 4, "Podest schodów", "Spocznik przy biegu schodów" },
                    { 5, "Przed windą", "Miejsce przed drzwiami windy" }
                });

            migrationBuilder.InsertData(
                table: "TypSali",
                columns: new[] { "Id", "Nazwa", "Opis" },
                values: new object[,]
                {
                    { 1, "Sala komputerowa", "Pracownia ze stanowiskami komputerowymi" },
                    { 2, "Sala ćwiczeniowa", "Sala do zajęć ćwiczeniowych" },
                    { 3, "Sekretariat", "Sekretariat lub dziekanat" },
                    { 4, "Sala wykładowa", "Duża sala wykładowa" },
                    { 5, "Toaleta", "Toaleta" },
                    { 6, "Pomieszczenie techniczne", "Niedostępne dla studentów" }
                });

            migrationBuilder.InsertData(
                table: "Udogodnienie",
                columns: new[] { "Id", "Ikona", "Nazwa", "Opis" },
                values: new object[,]
                {
                    { 1, "elevator", "Winda", "Winda osobowa z sygnalizacją głosową" },
                    { 2, "ramp", "Pochylnia", "Podjazd dla wózków" },
                    { 3, "hearing", "Pętla indukcyjna", "Wspomaganie aparatów słuchowych" },
                    { 4, "braille", "Oznaczenia brajlowskie", "Tabliczki z pismem Braille'a" },
                    { 5, "path", "Ścieżka dotykowa", "Prowadząca faktura w posadzce" },
                    { 6, "contrast", "Oznaczenie kontrastowe", "Kontrastowe oznaczenie krawędzi" }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Budynek_Kod",
                table: "Budynek",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kierunek_KierunekPowrotnyId",
                table: "Kierunek",
                column: "KierunekPowrotnyId");

            migrationBuilder.CreateIndex(
                name: "IX_Kierunek_PunktDocelowyId",
                table: "Kierunek",
                column: "PunktDocelowyId");

            migrationBuilder.CreateIndex(
                name: "IX_Kierunek_SalaDocelowaId",
                table: "Kierunek",
                column: "SalaDocelowaId");

            migrationBuilder.CreateIndex(
                name: "UQ_Kierunek_Zrodlo_Azymut",
                table: "Kierunek",
                columns: new[] { "PunktZrodlowyId", "Azymut" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ObszarAktywny_KierunekId",
                table: "ObszarAktywny",
                column: "KierunekId");

            migrationBuilder.CreateIndex(
                name: "IX_ObszarAktywny_ZdjecieId",
                table: "ObszarAktywny",
                column: "ZdjecieId");

            migrationBuilder.CreateIndex(
                name: "UQ_Pietro_Budynek_Numer",
                table: "Pietro",
                columns: new[] { "BudynekId", "Numer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunktRuchu_PietroId",
                table: "PunktRuchu",
                column: "PietroId");

            migrationBuilder.CreateIndex(
                name: "IX_PunktRuchu_TypPunktuId",
                table: "PunktRuchu",
                column: "TypPunktuId");

            migrationBuilder.CreateIndex(
                name: "UQ_PunktRuchu_Kod",
                table: "PunktRuchu",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunktUdogodnienie_UdogodnienieId",
                table: "PunktUdogodnienie",
                column: "UdogodnienieId");

            migrationBuilder.CreateIndex(
                name: "IX_Sala_PunktWejsciowyId",
                table: "Sala",
                column: "PunktWejsciowyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sala_Symbol",
                table: "Sala",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_Sala_TypSaliId",
                table: "Sala",
                column: "TypSaliId");

            migrationBuilder.CreateIndex(
                name: "UQ_Sala_Pietro_Symbol",
                table: "Sala",
                columns: new[] { "PietroId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaUdogodnienie_UdogodnienieId",
                table: "SalaUdogodnienie",
                column: "UdogodnienieId");

            migrationBuilder.CreateIndex(
                name: "UQ_TypPunktu_Nazwa",
                table: "TypPunktu",
                column: "Nazwa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TypSali_Nazwa",
                table: "TypSali",
                column: "Nazwa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Udogodnienie_Nazwa",
                table: "Udogodnienie",
                column: "Nazwa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utrudnienie_KierunekId",
                table: "Utrudnienie",
                column: "KierunekId");

            migrationBuilder.CreateIndex(
                name: "IX_Utrudnienie_PunktRuchuId",
                table: "Utrudnienie",
                column: "PunktRuchuId");

            migrationBuilder.CreateIndex(
                name: "IX_WpisAudytu_Data",
                table: "WpisAudytu",
                column: "DataOperacji",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ZapytanieTrasy_SalaDoId",
                table: "ZapytanieTrasy",
                column: "SalaDoId");

            migrationBuilder.CreateIndex(
                name: "IX_ZapytanieTrasy_SalaZId",
                table: "ZapytanieTrasy",
                column: "SalaZId");

            migrationBuilder.CreateIndex(
                name: "IX_ZapytanieTrasy_Sukces",
                table: "ZapytanieTrasy",
                columns: new[] { "CzySukces", "DataZapytania" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Zdjecie_PunktRuchuId",
                table: "Zdjecie",
                column: "PunktRuchuId");

            migrationBuilder.CreateIndex(
                name: "IX_Zdjecie_SalaId",
                table: "Zdjecie",
                column: "SalaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObszarAktywny");

            migrationBuilder.DropTable(
                name: "PunktUdogodnienie");

            migrationBuilder.DropTable(
                name: "SalaUdogodnienie");

            migrationBuilder.DropTable(
                name: "Utrudnienie");

            migrationBuilder.DropTable(
                name: "WpisAudytu");

            migrationBuilder.DropTable(
                name: "ZapytanieTrasy");

            migrationBuilder.DropTable(
                name: "ZgloszenieDostepnosci");

            migrationBuilder.DropTable(
                name: "Zdjecie");

            migrationBuilder.DropTable(
                name: "Udogodnienie");

            migrationBuilder.DropTable(
                name: "Kierunek");

            migrationBuilder.DropTable(
                name: "Sala");

            migrationBuilder.DropTable(
                name: "PunktRuchu");

            migrationBuilder.DropTable(
                name: "TypSali");

            migrationBuilder.DropTable(
                name: "Pietro");

            migrationBuilder.DropTable(
                name: "TypPunktu");

            migrationBuilder.DropTable(
                name: "Budynek");
        }
    }
}
