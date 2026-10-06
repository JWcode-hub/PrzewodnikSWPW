using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>
/// Dostęp do danych dla panelu administratora. Cienka warstwa nad DbContext — reguły biznesowe
/// (unikalność, zależności, kierunki powrotne) są w AdministracjaService.
/// </summary>
public interface IAdministracjaRepozytorium
{
    IQueryable<T> Zapytanie<T>() where T : class;
    Task<T?> ZnajdzAsync<T>(int id, CancellationToken ct = default) where T : class;
    void Dodaj<T>(T encja) where T : class;
    void Usun<T>(T encja) where T : class;
    Task ZapiszAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> RozpocznijTransakcjeAsync(CancellationToken ct = default);
}

public sealed class AdministracjaRepozytorium(PrzewodnikDbContext db) : IAdministracjaRepozytorium
{
    public IQueryable<T> Zapytanie<T>() where T : class => db.Set<T>();

    public async Task<T?> ZnajdzAsync<T>(int id, CancellationToken ct = default) where T : class =>
        await db.Set<T>().FindAsync([id], ct);

    public void Dodaj<T>(T encja) where T : class => db.Set<T>().Add(encja);

    public void Usun<T>(T encja) where T : class => db.Set<T>().Remove(encja);

    public Task ZapiszAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> RozpocznijTransakcjeAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);
}
