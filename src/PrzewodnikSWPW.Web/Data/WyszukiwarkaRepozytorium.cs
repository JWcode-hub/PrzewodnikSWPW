using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Data;

public interface IWyszukiwarkaRepozytorium
{
    /// <summary>
    /// Aktywne sale aktywnych budynków z piętrem, budynkiem i typem. Sal jest najwyżej kilkaset,
    /// więc normalizacja i dopasowanie odbywają się w pamięci (normalizacji nie da się wyrazić w SQL).
    /// </summary>
    Task<List<Sala>> PobierzAktywneSaleAsync(CancellationToken ct = default);

    Task ZapiszZapytanieAsync(ZapytanieTrasy zapytanie, CancellationToken ct = default);
}

public sealed class WyszukiwarkaRepozytorium(PrzewodnikDbContext db) : IWyszukiwarkaRepozytorium
{
    public Task<List<Sala>> PobierzAktywneSaleAsync(CancellationToken ct = default) =>
        db.Sale
            .AsNoTracking()
            .Include(s => s.Pietro).ThenInclude(p => p.Budynek)
            .Include(s => s.TypSali)
            .Where(s => s.CzyAktywna && s.Pietro.Budynek.CzyAktywny)
            .ToListAsync(ct);

    public async Task ZapiszZapytanieAsync(ZapytanieTrasy zapytanie, CancellationToken ct = default)
    {
        db.ZapytaniaTrasy.Add(zapytanie);
        await db.SaveChangesAsync(ct);
    }
}
