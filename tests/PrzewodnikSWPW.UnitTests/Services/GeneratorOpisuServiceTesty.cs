using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

public class GeneratorOpisuServiceTesty
{
    private static KrawedzGrafu K(int azymut, decimal waga, string? opis = null, RodzajPrzejscia rodzaj = RodzajPrzejscia.Korytarz, int? doSali = null) =>
        new(1, 1, doSali is null ? 2 : null, doSali, azymut, waga, rodzaj, true, rodzaj != RodzajPrzejscia.Schody, opis, []);

    private static readonly GrafBudynku Graf = new(1, [], [], [new SalaWGrafie(7, "A12", "Pracownia komputerowa")]);

    [Theory]
    [InlineData(0, "Idź prosto 6 metrów.")]
    [InlineData(90, "Skręć w prawo i idź 6 metrów.")]
    [InlineData(270, "Skręć w lewo i idź 6 metrów.")]
    [InlineData(180, "Zawróć i idź 6 metrów.")]
    public void RoznicaAzymutow_DajeWlasciwaInstrukcje(int azymut, string oczekiwana)
    {
        // Zwrot początkowy 0 — różnica azymutów równa azymutowi krawędzi.
        var instrukcje = new GeneratorOpisuService().Opisz(Graf, [K(azymut, 6)], zwrotPoczatkowy: 0);

        // Wiersz 0 to punkt startowy; krawędź opisuje wiersz 1.
        Assert.Equal(2, instrukcje.Count);
        Assert.Equal(oczekiwana, instrukcje[1].Tekst);
    }

    [Fact]
    public void KazdaInstrukcjaWzgledemZwrotuPoPoprzedniejKrawedzi()
    {
        // Północ (0), potem zachód (270) = w lewo, potem znowu zachód = prosto, potem północ = w prawo.
        var instrukcje = new GeneratorOpisuService().Opisz(Graf, [K(0, 8), K(270, 5), K(270, 6), K(0, 1)], zwrotPoczatkowy: 0);

        Assert.Equal(
            ["Idź prosto 8 metrów.", "Skręć w lewo i idź 5 metrów.", "Idź prosto 6 metrów.", "Skręć w prawo i idź 1 metr."],
            instrukcje.Skip(1).Select(i => i.Tekst));
    }

    [Fact]
    public void OpisPrzejsciaZBazy_JestDolaczany()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(Graf, [K(0, 12, opis: "Po drodze miniesz drzwi sali A12.")], 0);

        Assert.Equal("Idź prosto 12 metrów. Po drodze miniesz drzwi sali A12.", instrukcje[1].Tekst);
    }

    [Fact]
    public void Schody_SaOznaczone()
    {
        var instrukcja = new GeneratorOpisuService().Opisz(Graf, [K(0, 3, rodzaj: RodzajPrzejscia.Schody)], 0)[1];

        Assert.True(instrukcja.CzySchody);
        Assert.Contains("schody", instrukcja.Tekst);
    }

    [Fact]
    public void OstatniaKrawedzDoSali_MowiDoKtorejSaliWejsc()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(Graf, [K(90, 1, rodzaj: RodzajPrzejscia.Drzwi, doSali: 7)], 0, wstep: "Wyjdź z sali A16 na korytarz.");

        Assert.Equal(["Wyjdź z sali A16 na korytarz.", "Skręć w prawo i idź 1 metr. Wejdź do sali A12 — Pracownia komputerowa."],
            instrukcje.Select(i => i.Tekst));
    }

    // ---- Oś czasu trasy: nazwa punktu, metry, rodzaj przejścia i rola każdego wiersza ----

    /// <summary>Trzy punkty w linii i sala za drzwiami ostatniego: 1 → 2 → 3 → sala 7.</summary>
    private static readonly GrafBudynku GrafOsi = new(1,
        [
            new WezelGrafu(1, "Hol przy wejściu", 0, true, []),
            new WezelGrafu(2, "Skrzyżowanie korytarzy", 0, true, []),
            new WezelGrafu(3, "Korytarz przy sali A12", 0, true, []),
        ],
        [],
        [new SalaWGrafie(7, "A12", "Pracownia komputerowa")]);

    private static readonly KrawedzGrafu[] SciezkaOsi =
    [
        new(10, 1, 2, null, 0, 8m, RodzajPrzejscia.Korytarz, true, true, null, []),
        new(11, 2, 3, null, 270, 5.5m, RodzajPrzejscia.Schody, true, false, null, []),
        new(12, 3, null, 7, 0, 1m, RodzajPrzejscia.Drzwi, true, true, null, []),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LiczbaWierszy_RownaLiczbieKrawedziPlusJeden(int liczbaKrawedzi)
    {
        var instrukcje = new GeneratorOpisuService().Opisz(GrafOsi, SciezkaOsi[..liczbaKrawedzi], 0, punktStartowy: 1);

        Assert.Equal(liczbaKrawedzi + 1, instrukcje.Count);
    }

    [Fact]
    public void PierwszyWiersz_ToPunktStartowyZInstrukcjaWstepna_BezMetrowIRodzaju()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(GrafOsi, SciezkaOsi, 0, wstep: "Wyjdź z sali A16 na korytarz.");

        Assert.Equal(
            new Instrukcja("Wyjdź z sali A16 na korytarz.", false, "Hol przy wejściu", null, null, RolaKroku.Poczatek),
            instrukcje[0]);
    }

    [Fact]
    public void PierwszyWiersz_BezWstepu_MaInstrukcjeZNazwaPunktuStartowego()
    {
        var pierwszy = new GeneratorOpisuService().Opisz(GrafOsi, SciezkaOsi, 0)[0];

        Assert.Equal(RolaKroku.Poczatek, pierwszy.Rola);
        Assert.Equal("Zacznij w miejscu: Hol przy wejściu.", pierwszy.Tekst);
    }

    [Fact]
    public void Role_PierwszyPoczatek_OstatniCel_PozostalePosrednie()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(GrafOsi, SciezkaOsi, 0);

        Assert.Equal(
            [RolaKroku.Poczatek, RolaKroku.Posredni, RolaKroku.Posredni, RolaKroku.Cel],
            instrukcje.Select(i => i.Rola));
    }

    [Fact]
    public void Metry_ZgadzajaSieZWagamiKrawedzi_ARodzajZRodzajemPrzejscia()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(GrafOsi, SciezkaOsi, 0);

        Assert.Equal(SciezkaOsi.Select(k => (decimal?)k.Waga), instrukcje.Skip(1).Select(i => i.Metry));
        Assert.Equal(SciezkaOsi.Sum(k => k.Waga), instrukcje.Sum(i => i.Metry ?? 0));
        Assert.Equal(SciezkaOsi.Select(k => (RodzajPrzejscia?)k.Rodzaj), instrukcje.Skip(1).Select(i => i.Rodzaj));
    }

    [Fact]
    public void NazwaPunktu_ToCelKrawedzi_ADlaDrzwiSala()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(GrafOsi, SciezkaOsi, 0);

        Assert.Equal(
            ["Hol przy wejściu", "Skrzyżowanie korytarzy", "Korytarz przy sali A12", "Sala A12 — Pracownia komputerowa"],
            instrukcje.Select(i => i.NazwaPunktu));
    }

    [Fact]
    public void PustaSciezka_JedenWierszPoczatkowy()
    {
        var wiersz = Assert.Single(new GeneratorOpisuService().Opisz(GrafOsi, [], 0, punktStartowy: 2));

        Assert.Equal((RolaKroku.Poczatek, "Skrzyżowanie korytarzy"), (wiersz.Rola, wiersz.NazwaPunktu));
    }
}
