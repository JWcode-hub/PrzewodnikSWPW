using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Data;

public sealed record DaneGrafu(List<PunktRuchu> Punkty, List<Kierunek> Kierunki, List<Sala> Sale);

public interface ITrasaRepozytorium
{
    /// <summary>Wszystkie punkty, kierunki (z utrudnieniami) i sale budynku — jedno wczytanie dla GrafBudynkuCache.</summary>
    Task<DaneGrafu> PobierzDaneGrafuAsync(int budynekId, CancellationToken ct = default);

    /// <summary>Sale o podanych identyfikatorach z piętrem i budynkiem (także nieaktywne — decyduje serwis).</summary>
    Task<List<Sala>> PobierzSaleAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>Aktywne sale do list wyboru w formularzu trasy.</summary>
    Task<List<Sala>> PobierzAktywneSaleAsync(CancellationToken ct = default);

    Task ZapiszZapytanieAsync(ZapytanieTrasy zapytanie, CancellationToken ct = default);
}

public sealed class TrasaRepozytorium(PrzewodnikDbContext db) : ITrasaRepozytorium
{
    public async Task<DaneGrafu> PobierzDaneGrafuAsync(int budynekId, CancellationToken ct = default)
    {
        var punkty = await db.PunktyRuchu.AsNoTracking()
            .Include(p => p.Utrudnienia)
            .Where(p => p.Pietro.BudynekId == budynekId)
            .ToListAsync(ct);

        var kierunki = await db.Kierunki.AsNoTracking()
            .Include(k => k.Utrudnienia)
            .Where(k => k.PunktZrodlowy.Pietro.BudynekId == budynekId)
            .ToListAsync(ct);

        var sale = await db.Sale.AsNoTracking()
            .Where(s => s.Pietro.BudynekId == budynekId)
            .ToListAsync(ct);

        return new DaneGrafu(punkty, kierunki, sale);
    }

    public Task<List<Sala>> PobierzSaleAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        db.Sale.AsNoTracking()
            .Include(s => s.Pietro).ThenInclude(p => p.Budynek)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(ct);

    public Task<List<Sala>> PobierzAktywneSaleAsync(CancellationToken ct = default) =>
        db.Sale.AsNoTracking()
            .Include(s => s.Pietro).ThenInclude(p => p.Budynek)
            .Include(s => s.TypSali)
            .Where(s => s.CzyAktywna && s.Pietro.Budynek.CzyAktywny)
            .OrderBy(s => s.Pietro.Budynek.Kod).ThenBy(s => s.Pietro.Numer).ThenBy(s => s.Symbol)
            .ToListAsync(ct);

    public async Task ZapiszZapytanieAsync(ZapytanieTrasy zapytanie, CancellationToken ct = default)
    {
        db.ZapytaniaTrasy.Add(zapytanie);
        await db.SaveChangesAsync(ct);
    }
}
