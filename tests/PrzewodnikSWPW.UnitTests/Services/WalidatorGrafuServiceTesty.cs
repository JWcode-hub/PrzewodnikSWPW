using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>Reguły walidatora grafu (UC-19) na danych w pamięci — D-06, D-07, D-08.</summary>
public class WalidatorGrafuServiceTesty
{
    private static readonly Pietro Parter = new() { Id = 1, BudynekId = 1 };
    private static readonly Pietro ParterB = new() { Id = 2, BudynekId = 2 };

    private static PunktRuchu P(int id, string? opisGlosowy = null, Pietro? pietro = null, bool aktywny = true) =>
        new() { Id = id, Kod = $"P{id}", Nazwa = $"Punkt {id}", OpisGlosowy = opisGlosowy, Pietro = pietro ?? Parter, PietroId = (pietro ?? Parter).Id, CzyAktywny = aktywny };

    private static Kierunek K(int id, int z, int doPunktu, int azymut, decimal waga = 5, int? powrot = null) =>
        new() { Id = id, PunktZrodlowyId = z, PunktDocelowyId = doPunktu, Azymut = azymut, Waga = waga, KierunekPowrotnyId = powrot };

    private static Budynek B(int id, int? wejscie, bool aktywny = true) => new() { Id = id, Kod = $"B{id}", PunktWejsciaGlownegoId = wejscie, CzyAktywny = aktywny };

    // --- D-06 ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Po prawej stronie drzwi do sali A czternaście.")]
    [InlineData("Drzwi do sali A dwanaście są po twojej prawej stronie.")]
    [InlineData("Toaleta jest po lewej.")]
    [InlineData("Z lewej strony gablota.")]
    [InlineData("Na prawo schody.")]
    [InlineData("przycisk przywołania po prawej stronie drzwi windy")] // poprawne zdanie — ale reguła daje OSTRZEŻENIE, decyduje redaktor
    public void SlowaStronne_ZglaszaneJakoOstrzezenieZPodpowiedzia(string opis)
    {
        var uwaga = Assert.Single(WalidatorGrafuService.SprawdzSlowaStronne([P(1, opis)]));

        Assert.Equal(PoziomUwagi.Ostrzezenie, uwaga.Poziom);
        Assert.Equal(WalidatorGrafuService.RegulaSlowaStronne, uwaga.Regula);
        Assert.Contains("Wymień punkty orientacyjne bez stron", uwaga.Podpowiedz);
        Assert.Equal(RodzajRekordu.PunktRuchu, uwaga.Rekord);
    }

    [Theory]
    [InlineData("Są tu drzwi do sali A czternaście i do sali A piętnaście.")]
    [InlineData("Skrzyżowanie korytarzy. Na podłodze ścieżka dotykowa.")]
    [InlineData("Sprawna lewarkowa winda.")]   // „lewa” w środku słowa to nie słowo stronne
    public void BezSlowStronnych_BrakUwag(string opis)
    {
        Assert.Empty(WalidatorGrafuService.SprawdzSlowaStronne([P(1, opis)]));
    }

    [Fact]
    public void SlowaStronne_SprawdzaOpisIOpisGlosowy()
    {
        var p = P(1, "Po lewej toaleta.");
        p.Opis = "Po prawej okienko.";

        Assert.Equal(2, WalidatorGrafuService.SprawdzSlowaStronne([p]).Count());
    }

    // --- D-07 ---------------------------------------------------------------------------------------------

    [Fact]
    public void PoprawnaPara_BezUwag()
    {
        Assert.Empty(WalidatorGrafuService.SprawdzPowroty([P(1), P(2)], [K(1, 1, 2, 90, powrot: 2), K(2, 2, 1, 270, powrot: 1)]));
    }

    [Fact]
    public void WskazanieJednostronne_Blad()
    {
        var uwagi = WalidatorGrafuService.SprawdzPowroty([P(1), P(2)], [K(1, 1, 2, 90, powrot: 2), K(2, 2, 1, 270, powrot: null)]).ToList();

        Assert.Equal(WalidatorGrafuService.RegulaPowrotWzajemny, Assert.Single(uwagi).Regula);
        Assert.Equal(PoziomUwagi.Blad, uwagi[0].Poziom);
    }

    [Fact]
    public void KonceNieodwrocone_Blad()
    {
        // Powrót wskazany wzajemnie i z dobrym azymutem, ale prowadzi z innego punktu.
        var uwagi = WalidatorGrafuService.SprawdzPowroty([P(1), P(2), P(3)], [K(1, 1, 2, 90, powrot: 2), K(2, 3, 1, 270, powrot: 1)]).ToList();

        Assert.Contains(uwagi, u => u.Regula == WalidatorGrafuService.RegulaPowrotKonce && u.RekordId == 1);
    }

    [Fact]
    public void AzymutPowrotuInnyNizPlus180_Blad()
    {
        var uwagi = WalidatorGrafuService.SprawdzPowroty([P(1), P(2)], [K(1, 1, 2, 90, powrot: 2), K(2, 2, 1, 180, powrot: 1)]).ToList();

        Assert.Contains(uwagi, u => u.Regula == WalidatorGrafuService.RegulaPowrotAzymut && u.RekordId == 1 && u.Tresc.Contains("270°"));
    }

    [Theory]
    [InlineData(3, 4, false)]    // 33% — schody w górę mogą kosztować więcej, to normalne
    [InlineData(4, 6, false)]    // dokładnie 50% — jeszcze nie
    [InlineData(4, 6.5, true)]   // 62,5%
    [InlineData(10, 3, true)]    // kierunek bez znaczenia
    public void RoznicaWag_OstrzezenieDopieroPowyzej50Procent(decimal tam, decimal zPowrotem, bool ostrzezenie)
    {
        var uwagi = WalidatorGrafuService.SprawdzPowroty([P(1), P(2)], [K(1, 1, 2, 90, tam, powrot: 2), K(2, 2, 1, 270, zPowrotem, powrot: 1)]).ToList();

        Assert.Equal(ostrzezenie ? 1 : 0, uwagi.Count(u => u.Regula == WalidatorGrafuService.RegulaPowrotWaga));
        Assert.All(uwagi, u => Assert.Equal(PoziomUwagi.Ostrzezenie, u.Poziom)); // para zgłaszana raz i tylko jako ostrzeżenie
    }

    // --- D-08 ---------------------------------------------------------------------------------------------

    [Fact]
    public void AktywnyBudynekBezWejscia_Blad_NieaktywnyBezUwag()
    {
        var uwagi = WalidatorGrafuService.SprawdzWejsciaIOsiagalnosc([B(1, null), B(2, null, aktywny: false)], [P(1)], []).ToList();

        var uwaga = Assert.Single(uwagi);
        Assert.Equal((WalidatorGrafuService.RegulaWejscieBrak, RodzajRekordu.Budynek, 1), (uwaga.Regula, uwaga.Rekord, uwaga.RekordId));
    }

    [Fact]
    public void WejscieWInnymBudynku_Blad()
    {
        var uwagi = WalidatorGrafuService.SprawdzWejsciaIOsiagalnosc([B(1, wejscie: 9)], [P(1), P(9, pietro: ParterB)], []).ToList();

        Assert.Equal(WalidatorGrafuService.RegulaWejscieInnyBudynek, Assert.Single(uwagi).Regula);
    }

    [Fact]
    public void PunktOdcietyOdWejscia_Blad_NieaktywnyNieZglaszany()
    {
        // 1 ↔ 2 połączone; 3 bez żadnej krawędzi; 4 nieaktywny i odcięty; 5 osiągalny tylko krawędzią nieaktywną.
        var punkty = new[] { P(1), P(2), P(3), P(4, aktywny: false), P(5) };
        var kierunki = new List<Kierunek> { K(1, 1, 2, 90), K(2, 2, 1, 270), K(3, 2, 5, 0) };
        kierunki[2].CzyAktywny = false;

        var nieosiagalne = WalidatorGrafuService.SprawdzWejsciaIOsiagalnosc([B(1, wejscie: 1)], punkty, kierunki)
            .Where(u => u.Regula == WalidatorGrafuService.RegulaNieosiagalny).Select(u => u.RekordId).OrderBy(x => x);

        Assert.Equal([3, 5], nieosiagalne);
    }

    // --- UC-19: osiem reguł na poprawnym grafie z jednym wstrzykniętym błędem ---------------------------------

    /// <summary>
    /// Poprawny graf: wejście P1 ↔ P2 ↔ P3 (pary powrotne wskazane wzajemnie), z P2 kierunek do sali S1,
    /// sala z punktem wejściowym, zdjęcie informacyjne z dobrym tekstem alternatywnym. Walidator: zero uwag.
    /// </summary>
    private sealed class Graf
    {
        public List<Budynek> Budynki { get; } = [B(1, wejscie: 1)];
        public List<PunktRuchu> Punkty { get; } = [P(1, "Wejście główne, przed tobą hol."), P(2, "Hol przy portierni."), P(3, "Koniec korytarza przy oknie.")];
        public List<Kierunek> Kierunki { get; } =
        [
            K(1, 1, 2, 0, powrot: 2), K(2, 2, 1, 180, powrot: 1),
            K(3, 2, 3, 90, powrot: 4), K(4, 3, 2, 270, powrot: 3),
            new() { Id = 5, PunktZrodlowyId = 2, SalaDocelowaId = 1, Azymut = 270, Waga = 2 },
        ];
        public List<Sala> Sale { get; } = [new() { Id = 1, Symbol = "A15", Nazwa = "Sala A15", PunktWejsciowyId = 2 }];
        public List<Zdjecie> Zdjecia { get; } =
            [new() { Id = 1, PunktRuchuId = 2, SciezkaPliku = "media/hol.jpg", TekstAlternatywny = "Hol z portiernią i ławkami pod oknem." }];

        public IReadOnlyList<UwagaWalidatora> Waliduj() => WalidatorGrafuService.Waliduj(Budynki, Punkty, Kierunki, Sale, Zdjecia);
        public Kierunek Kierunek(int id) => Kierunki.Single(k => k.Id == id);
    }

    private static UwagaWalidatora JednaUwaga(Graf graf, string regula)
    {
        var uwaga = Assert.Single(graf.Waliduj(), u => u.Regula == regula);
        return uwaga;
    }

    [Fact]
    public void PoprawnyGraf_BezUwag()
    {
        Assert.Empty(new Graf().Waliduj());
    }

    [Fact]
    public void Regula1_PunktBezAktywnejKrawedziWychodzacej_Blad()
    {
        var graf = new Graf();
        graf.Punkty.Add(P(4, "Wnęka z automatem z napojami."));
        graf.Kierunki.Add(K(6, 3, 4, 0)); // do P4 da się wejść, ale P4 nie ma żadnego wyjścia

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaSlepyZaulek);

        Assert.Equal((PoziomUwagi.Blad, RodzajRekordu.PunktRuchu, 4), (uwaga.Poziom, uwaga.Rekord, uwaga.RekordId));
    }

    [Fact]
    public void Regula1_JedynaKrawedzNieaktywna_TezSlepyZaulek()
    {
        var graf = new Graf();
        graf.Kierunek(4).CzyAktywny = false; // P3 ma tylko nieaktywne wyjście

        Assert.Equal(3, JednaUwaga(graf, WalidatorGrafuService.RegulaSlepyZaulek).RekordId);
    }

    [Fact]
    public void Regula2_KrawedzBezParyPowrotnej_Blad()
    {
        var graf = new Graf();
        graf.Kierunki.RemoveAll(k => k.Id == 4); // P2 → P3 zostaje, P3 → P2 znika
        graf.Kierunek(3).KierunekPowrotnyId = null;

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaBrakPowrotu);

        Assert.Equal((PoziomUwagi.Blad, RodzajRekordu.Kierunek, 3), (uwaga.Poziom, uwaga.Rekord, uwaga.RekordId));
        Assert.Contains("azymucie 270°", uwaga.Podpowiedz); // 90° + 180°
    }

    [Theory]
    [InlineData(0, PoziomUwagi.Blad)]
    [InlineData(-3, PoziomUwagi.Blad)]
    [InlineData(100.5, PoziomUwagi.Ostrzezenie)]
    [InlineData(150, PoziomUwagi.Ostrzezenie)]
    public void Regula3_WagaNiedodatniaLubPonad100Metrow(decimal waga, PoziomUwagi poziom)
    {
        var graf = new Graf();
        graf.Kierunek(5).Waga = waga; // krawędź do sali — bez pary powrotnej, więc nie rusza reguły D-07 o różnicy wag

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaWaga);

        Assert.Equal((poziom, 5), (uwaga.Poziom, uwaga.RekordId));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(0.5)]
    public void Regula3_WagaNaGranicy_BezUwag(decimal waga)
    {
        var graf = new Graf();
        graf.Kierunek(5).Waga = waga;

        Assert.Empty(graf.Waliduj());
    }

    [Fact]
    public void Regula4_DwieKrawedzieZJednegoPunktuOTymSamymAzymucie_Blad()
    {
        var graf = new Graf();
        graf.Kierunek(5).Azymut = 90; // z P2 na 90° prowadzi już kierunek do P3

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaKonfliktAzymutu);

        Assert.Equal((PoziomUwagi.Blad, 5), (uwaga.Poziom, uwaga.RekordId)); // zgłaszana druga krawędź, pierwsza zostaje
        Assert.Contains("2 kierunki o azymucie 90°", uwaga.Tresc);
    }

    [Fact]
    public void Regula5_SalaBezPunktuWejsciowego_Blad_NieaktywnaBezUwag()
    {
        var graf = new Graf();
        graf.Sale[0].PunktWejsciowyId = null;
        graf.Sale.Add(new Sala { Id = 2, Symbol = "A16", Nazwa = "Magazyn", CzyAktywna = false });

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaSalaBezWejscia);

        Assert.Equal((PoziomUwagi.Blad, RodzajRekordu.Sala, 1, "sala A15"), (uwaga.Poziom, uwaga.Rekord, uwaga.RekordId, uwaga.OpisRekordu));
    }

    [Fact]
    public void Regula6_PunktNieosiagalnyZWejsciaGlownego_Blad()
    {
        var graf = new Graf();
        // Wyspa P4 ↔ P5: każda krawędź ma parę, żaden punkt nie jest ślepym zaułkiem — ale z wejścia tam nie dojdziesz.
        graf.Punkty.AddRange([P(4, "Zaplecze techniczne za kotłownią."), P(5, "Korytarz przy magazynie.")]);
        graf.Kierunki.AddRange([K(6, 4, 5, 0, powrot: 7), K(7, 5, 4, 180, powrot: 6)]);

        var nieosiagalne = graf.Waliduj().Where(u => u.Regula == WalidatorGrafuService.RegulaNieosiagalny).ToList();

        Assert.Equal([4, 5], nieosiagalne.Select(u => u.RekordId).Order());
        Assert.All(nieosiagalne, u => Assert.Equal(PoziomUwagi.Blad, u.Poziom));
    }

    [Theory]
    [InlineData("Hol.")]
    [InlineData("Zdjęcie holu")]   // 12 znaków
    [InlineData("   ")]
    public void Regula7_ZdjecieInformacyjneZKrotkimTekstemAlternatywnym_Ostrzezenie(string alt)
    {
        var graf = new Graf();
        graf.Zdjecia[0].TekstAlternatywny = alt;

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaTekstAlternatywny);

        Assert.Equal((PoziomUwagi.Ostrzezenie, RodzajRekordu.Zdjecie, 1), (uwaga.Poziom, uwaga.Rekord, uwaga.RekordId));
        Assert.Equal("zdjęcie nr 1 punktu P2", uwaga.OpisRekordu); // link do formularza zdjęcia, opis wskazuje miejsce
    }

    [Fact]
    public void Regula7_ZdjecieDekoracyjneBezTekstu_BezUwag()
    {
        var graf = new Graf();
        graf.Zdjecia[0].TekstAlternatywny = "";
        graf.Zdjecia[0].CzyDekoracyjne = true;

        Assert.Empty(graf.Waliduj());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Hol.")]
    public void Regula8_PunktBezOpisuGlosowego_Ostrzezenie(string? opisGlosowy)
    {
        var graf = new Graf();
        graf.Punkty[2].OpisGlosowy = opisGlosowy;

        var uwaga = JednaUwaga(graf, WalidatorGrafuService.RegulaOpisGlosowy);

        Assert.Equal((PoziomUwagi.Ostrzezenie, RodzajRekordu.PunktRuchu, 3), (uwaga.Poziom, uwaga.Rekord, uwaga.RekordId));
    }

    [Fact]
    public void Waliduj_BledyPrzedOstrzezeniami()
    {
        // Budynek bez wejścia i punkt bez wyjścia (błędy) oraz słowo stronne (ostrzeżenie).
        var uwagi = WalidatorGrafuService.Waliduj([B(1, null)], [P(1, "Po lewej gablota.")], []);

        Assert.Equal([PoziomUwagi.Blad, PoziomUwagi.Blad, PoziomUwagi.Ostrzezenie], uwagi.Select(u => u.Poziom));
    }
}
