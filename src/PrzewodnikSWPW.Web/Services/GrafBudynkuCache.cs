using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using PrzewodnikSWPW.Web.Data;

namespace PrzewodnikSWPW.Web.Services;

public interface IGrafBudynkuCache
{
    Task<GrafBudynku> PobierzGrafAsync(int budynekId, CancellationToken ct = default);

    /// <summary>
    /// Usuwa graf z pamięci. Wywoływać po KAŻDEJ zmianie punktów, kierunków, sal i utrudnień
    /// w panelu administratora; <c>null</c> — wszystkie budynki.
    /// </summary>
    void Uniewaznij(int? budynekId = null);
}

/// <summary>
/// Graf budynku w IMemoryCache na 10 minut (cache-aside, 05_ARCHITEKTURA_I_DEPLOY.md §2).
/// Singleton — repozytorium (scoped, z DbContext) pobiera z nowego zakresu przy każdym wczytaniu.
/// </summary>
public sealed class GrafBudynkuCache(IMemoryCache cache, IServiceScopeFactory zakresy) : IGrafBudynkuCache
{
    public static readonly TimeSpan CzasZycia = TimeSpan.FromMinutes(10);

    // Jeden token unieważnienia na budynek — Uniewaznij() anuluje go i wpis natychmiast wypada z pamięci.
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _tokeny = new();

    public async Task<GrafBudynku> PobierzGrafAsync(int budynekId, CancellationToken ct = default)
    {
        if (cache.TryGetValue(Klucz(budynekId), out GrafBudynku? graf) && graf is not null)
        {
            return graf;
        }

        graf = await WczytajAsync(budynekId, ct);

        var token = _tokeny.GetOrAdd(budynekId, _ => new CancellationTokenSource());
        cache.Set(Klucz(budynekId), graf, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CzasZycia,
            ExpirationTokens = { new CancellationChangeToken(token.Token) },
        });
        return graf;
    }

    public void Uniewaznij(int? budynekId = null)
    {
        var budynki = budynekId is int id ? [id] : _tokeny.Keys.ToArray();
        foreach (var b in budynki)
        {
            if (_tokeny.TryRemove(b, out var zrodlo))
            {
                zrodlo.Cancel();
                zrodlo.Dispose();
            }
            cache.Remove(Klucz(b));
        }
    }

    private async Task<GrafBudynku> WczytajAsync(int budynekId, CancellationToken ct)
    {
        await using var zakres = zakresy.CreateAsyncScope();
        var repozytorium = zakres.ServiceProvider.GetRequiredService<ITrasaRepozytorium>();
        var dane = await repozytorium.PobierzDaneGrafuAsync(budynekId, ct);

        return new GrafBudynku(
            budynekId,
            dane.Punkty.Select(p => new WezelGrafu(p.Id, p.Nazwa, p.AzymutDomyslny, p.CzyAktywny,
                p.Utrudnienia.Select(Okres).ToList())),
            dane.Kierunki.Select(k => new KrawedzGrafu(k.Id, k.PunktZrodlowyId, k.PunktDocelowyId, k.SalaDocelowaId,
                k.Azymut, k.Waga, k.RodzajPrzejscia, k.CzyAktywny, k.CzyDostepnyBezSchodow, k.OpisPrzejscia,
                k.Utrudnienia.Select(Okres).ToList())),
            dane.Sale.Select(s => new SalaWGrafie(s.Id, s.Symbol, s.Nazwa)));
    }

    private static OkresUtrudnienia Okres(Models.Utrudnienie u) => new(u.ObowiazujeOd, u.ObowiazujeDo, u.Przyczyna);

    private static string Klucz(int budynekId) => $"graf-budynku:{budynekId}";
}
