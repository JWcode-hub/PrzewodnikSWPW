using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;

namespace PrzewodnikSWPW.UnitTests.Data;

/// <summary>
/// Test integracyjny spójności grafu po zasianiu danych budynku A (ryzyko P-07, UC-19).
/// Wymaga SQL Server — patrz <see cref="BazaTestowaFixture"/>.
/// </summary>
[Collection(BazaTestowaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class DbSeederTesty(BazaTestowaFixture baza)
{
    [Fact]
    public async Task Seeder_WstawiaDaneZgodneZPlikiemSql()
    {
        await using var db = baza.UtworzKontekst();

        Assert.Equal(1, await db.Budynki.CountAsync());
        Assert.Equal(1, await db.Pietra.CountAsync());
        Assert.Equal(9, await db.PunktyRuchu.CountAsync());
        Assert.Equal(5, await db.Sale.CountAsync());
        Assert.Equal(22, await db.Kierunki.CountAsync());
        Assert.Equal(4, await db.PunktUdogodnienia.CountAsync());
        Assert.Equal(1, await db.SalaUdogodnienia.CountAsync());

        var nieaktywny = await db.Kierunki.SingleAsync(k => !k.CzyAktywny);
        Assert.Equal("Po lewej stronie nie ma przejścia. Jest tam ściana z gablotą informacyjną.", nieaktywny.OpisPrzejscia);
    }

    [Fact]
    public async Task KazdyPunktRuchu_MaCoNajmniejJednaAktywnaKrawedzWychodzaca()
    {
        await using var db = baza.UtworzKontekst();

        var punktyBezWyjscia = await db.PunktyRuchu
            .Where(p => !p.KierunkiWychodzace.Any(k => k.CzyAktywny))
            .Select(p => p.Kod)
            .ToListAsync();

        Assert.Empty(punktyBezWyjscia);
    }

    [Fact]
    public async Task KazdaKrawedzDoPunktu_MaKrawedzPowrotnaWskazanaWKierunekPowrotnyId()
    {
        await using var db = baza.UtworzKontekst();
        var krawedzie = await db.Kierunki.AsNoTracking()
            .Where(k => k.PunktDocelowyId != null)
            .ToListAsync();
        var poId = krawedzie.ToDictionary(k => k.Id);

        Assert.NotEmpty(krawedzie);
        Assert.All(krawedzie, k =>
        {
            Assert.True(k.KierunekPowrotnyId.HasValue, $"Krawędź {k.Id} nie ma ustawionego KierunekPowrotnyId.");
            var powrotna = poId[k.KierunekPowrotnyId!.Value];
            Assert.Equal(k.PunktDocelowyId, powrotna.PunktZrodlowyId);
            Assert.Equal(k.PunktZrodlowyId, powrotna.PunktDocelowyId);
            Assert.Equal((k.Azymut + 180) % 360, powrotna.Azymut);
        });
    }

    [Fact]
    public async Task KazdaSala_MaUstawionyPunktWejsciowy()
    {
        await using var db = baza.UtworzKontekst();

        var saleBezWejscia = await db.Sale
            .Where(s => s.PunktWejsciowyId == null)
            .Select(s => s.Symbol)
            .ToListAsync();

        Assert.Empty(saleBezWejscia);
    }

    [Fact]
    public async Task GrafJestSpojny_ZWejsciaGlownegoOsiagalnyKazdyPunkt()
    {
        await using var db = baza.UtworzKontekst();
        var wszystkiePunkty = await db.PunktyRuchu.ToDictionaryAsync(p => p.Id, p => p.Kod);
        var sasiedzi = (await db.Kierunki
                .Where(k => k.CzyAktywny && k.PunktDocelowyId != null)
                .Select(k => new { k.PunktZrodlowyId, PunktDocelowyId = k.PunktDocelowyId!.Value })
                .ToListAsync())
            .ToLookup(k => k.PunktZrodlowyId, k => k.PunktDocelowyId);
        // Start z wejścia głównego zapisanego przy budynku (D-08), nie ze stałej w kodzie.
        var budynek = await db.Budynki.Include(b => b.PunktWejsciaGlownego).SingleAsync();
        Assert.Equal("A-0-P01", budynek.PunktWejsciaGlownego?.Kod);
        var start = budynek.PunktWejsciaGlownegoId!.Value;

        // BFS po aktywnych krawędziach między punktami ruchu.
        var odwiedzone = new HashSet<int> { start };
        var kolejka = new Queue<int>([start]);
        while (kolejka.TryDequeue(out var biezacy))
        {
            foreach (var nastepny in sasiedzi[biezacy])
            {
                if (odwiedzone.Add(nastepny))
                {
                    kolejka.Enqueue(nastepny);
                }
            }
        }

        var nieosiagalne = wszystkiePunkty.Keys.Except(odwiedzone).Select(id => wszystkiePunkty[id]);
        Assert.Empty(nieosiagalne);
    }

    [Fact]
    public async Task PonowneZasianie_NieDublujeDanych()
    {
        await using var db = baza.UtworzKontekst();

        var wstawiono = await DbSeeder.ZasiejAsync(db);

        Assert.False(wstawiono);
        Assert.Equal(9, await db.PunktyRuchu.CountAsync());
    }
}
