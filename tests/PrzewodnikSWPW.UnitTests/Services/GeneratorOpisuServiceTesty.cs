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

        Assert.Equal(oczekiwana, Assert.Single(instrukcje).Tekst);
    }

    [Fact]
    public void KazdaInstrukcjaWzgledemZwrotuPoPoprzedniejKrawedzi()
    {
        // Północ (0), potem zachód (270) = w lewo, potem znowu zachód = prosto, potem północ = w prawo.
        var instrukcje = new GeneratorOpisuService().Opisz(Graf, [K(0, 8), K(270, 5), K(270, 6), K(0, 1)], zwrotPoczatkowy: 0);

        Assert.Equal(
            ["Idź prosto 8 metrów.", "Skręć w lewo i idź 5 metrów.", "Idź prosto 6 metrów.", "Skręć w prawo i idź 1 metr."],
            instrukcje.Select(i => i.Tekst));
    }

    [Fact]
    public void OpisPrzejsciaZBazy_JestDolaczany()
    {
        var instrukcje = new GeneratorOpisuService().Opisz(Graf, [K(0, 12, opis: "Po drodze miniesz drzwi sali A12.")], 0);

        Assert.Equal("Idź prosto 12 metrów. Po drodze miniesz drzwi sali A12.", instrukcje[0].Tekst);
    }

    [Fact]
    public void Schody_SaOznaczone()
    {
        var instrukcja = new GeneratorOpisuService().Opisz(Graf, [K(0, 3, rodzaj: RodzajPrzejscia.Schody)], 0)[0];

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
}
