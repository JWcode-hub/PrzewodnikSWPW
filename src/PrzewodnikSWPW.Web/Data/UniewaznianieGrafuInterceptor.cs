using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>
/// Unieważnia graf budynku w pamięci po każdej zapisanej zmianie danych, z których jest zbudowany
/// (punkty, kierunki, sale, utrudnienia, piętra, budynki). Działa dla każdego zapisu — panelu
/// administratora, seedera i importu — więc nie da się o nim zapomnieć w nowym kontrolerze.
/// </summary>
public sealed class UniewaznianieGrafuInterceptor(IGrafBudynkuCache grafy) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> TypyGrafu =
        [typeof(PunktRuchu), typeof(Kierunek), typeof(Sala), typeof(Utrudnienie), typeof(Pietro), typeof(Budynek)];

    private bool _zmienionoGraf;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData dane, InterceptionResult<int> wynik, CancellationToken ct = default)
    {
        Sprawdz(dane.Context);
        return base.SavingChangesAsync(dane, wynik, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData dane, InterceptionResult<int> wynik)
    {
        Sprawdz(dane.Context);
        return base.SavingChanges(dane, wynik);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData dane, int wynik, CancellationToken ct = default)
    {
        Uniewaznij();
        return base.SavedChangesAsync(dane, wynik, ct);
    }

    public override int SavedChanges(SaveChangesCompletedEventData dane, int wynik)
    {
        Uniewaznij();
        return base.SavedChanges(dane, wynik);
    }

    private void Sprawdz(DbContext? kontekst) =>
        _zmienionoGraf |= kontekst?.ChangeTracker.Entries().Any(e =>
            TypyGrafu.Contains(e.Entity.GetType()) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) == true;

    private void Uniewaznij()
    {
        if (_zmienionoGraf)
        {
            grafy.Uniewaznij();
            _zmienionoGraf = false;
        }
    }
}
