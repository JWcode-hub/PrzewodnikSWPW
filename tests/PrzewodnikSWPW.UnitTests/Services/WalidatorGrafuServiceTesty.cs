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

    [Fact]
    public void Waliduj_BledyPrzedOstrzezeniami()
    {
        var uwagi = WalidatorGrafuService.Waliduj([B(1, null)], [P(1, "Po lewej gablota.")], []);

        Assert.Equal([PoziomUwagi.Blad, PoziomUwagi.Ostrzezenie], uwagi.Select(u => u.Poziom));
    }
}
