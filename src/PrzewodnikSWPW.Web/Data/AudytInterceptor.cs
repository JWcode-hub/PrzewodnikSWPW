using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>
/// Rejestr zmian (WF-28, [U] pkt 10c): przy każdym SaveChanges zapisuje w WpisAudytu, kto, kiedy,
/// którą encję i jak zmienił — wartości stare i nowe jako JSON.
/// <list type="bullet">
/// <item>Tylko encje domenowe. Tabel Identity nie audytujemy — JSON zawierałby skróty haseł i znaczniki bezpieczeństwa.</item>
/// <item>Bez ZapytanieTrasy i WpisAudytu — to same w sobie dzienniki.</item>
/// <item>Dane osobowe ze zgłoszeń dostępności są maskowane (RODO — minimalizacja, P-20).</item>
/// </list>
/// Wpisy dla nowych rekordów powstają PO zapisie, bo dopiero wtedy znany jest klucz (IDENTITY).
/// </summary>
public sealed class AudytInterceptor(IBiezacyUzytkownik uzytkownik, TimeProvider czas) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> Pomijane = [typeof(WpisAudytu), typeof(ZapytanieTrasy)];

    private static readonly Dictionary<Type, HashSet<string>> Maskowane = new()
    {
        [typeof(ZgloszenieDostepnosci)] = [nameof(ZgloszenieDostepnosci.ImieNazwisko), nameof(ZgloszenieDostepnosci.Email), nameof(ZgloszenieDostepnosci.Telefon)],
    };

    private static readonly JsonSerializerOptions OpcjeJson = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private sealed record Zmiana(EntityEntry Wpis, string Operacja, Dictionary<string, object?>? Stare, Dictionary<string, object?>? Nowe);

    private List<Zmiana> _oczekujace = [];
    private bool _zapisujeAudyt;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData dane, InterceptionResult<int> wynik, CancellationToken ct = default)
    {
        Zbierz(dane.Context);
        return base.SavingChangesAsync(dane, wynik, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData dane, InterceptionResult<int> wynik)
    {
        Zbierz(dane.Context);
        return base.SavingChanges(dane, wynik);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData dane, int wynik, CancellationToken ct = default)
    {
        if (DodajWpisy(dane.Context))
        {
            _zapisujeAudyt = true;
            try { await dane.Context!.SaveChangesAsync(ct); }
            finally { _zapisujeAudyt = false; }
        }
        return await base.SavedChangesAsync(dane, wynik, ct);
    }

    public override int SavedChanges(SaveChangesCompletedEventData dane, int wynik)
    {
        if (DodajWpisy(dane.Context))
        {
            _zapisujeAudyt = true;
            try { dane.Context!.SaveChanges(); }
            finally { _zapisujeAudyt = false; }
        }
        return base.SavedChanges(dane, wynik);
    }

    public override void SaveChangesFailed(DbContextErrorEventData dane) => _oczekujace = [];

    public override Task SaveChangesFailedAsync(DbContextErrorEventData dane, CancellationToken ct = default)
    {
        _oczekujace = [];
        return Task.CompletedTask;
    }

    private void Zbierz(DbContext? kontekst)
    {
        if (kontekst is null || _zapisujeAudyt)
        {
            return;
        }

        kontekst.ChangeTracker.DetectChanges();
        _oczekujace = kontekst.ChangeTracker.Entries()
            .Where(e => e.Entity.GetType().Namespace == typeof(Budynek).Namespace && !Pomijane.Contains(e.Entity.GetType()))
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.State switch
            {
                EntityState.Added => new Zmiana(e, "INSERT", null, null),
                EntityState.Deleted => new Zmiana(e, "DELETE", Wartosci(e, e.Properties, stare: true), null),
                _ => new Zmiana(e, "UPDATE",
                    Wartosci(e, e.Properties.Where(p => p.IsModified), stare: true),
                    Wartosci(e, e.Properties.Where(p => p.IsModified), stare: false)),
            })
            .Where(z => z.Operacja != "UPDATE" || z.Nowe!.Count > 0)
            .ToList();
    }

    /// <summary>Dodaje wpisy audytu do kontekstu; <c>true</c>, gdy jest co zapisać.</summary>
    private bool DodajWpisy(DbContext? kontekst)
    {
        if (kontekst is null || _zapisujeAudyt || _oczekujace.Count == 0)
        {
            return false;
        }

        var teraz = czas.GetLocalNow().DateTime;
        foreach (var z in _oczekujace)
        {
            kontekst.Set<WpisAudytu>().Add(new WpisAudytu
            {
                UzytkownikId = uzytkownik.Id,
                Encja = z.Wpis.Metadata.ClrType.Name,
                KluczEncji = string.Join(",", z.Wpis.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue)),
                Operacja = z.Operacja,
                WartosciStare = z.Stare is null ? null : JsonSerializer.Serialize(z.Stare, OpcjeJson),
                // Dla INSERT wartości czytamy dopiero teraz — po zapisie znamy wygenerowany klucz.
                WartosciNowe = z.Operacja == "DELETE"
                    ? null
                    : JsonSerializer.Serialize(z.Nowe ?? Wartosci(z.Wpis, z.Wpis.Properties, stare: false), OpcjeJson),
                DataOperacji = teraz,
            });
        }
        _oczekujace = [];
        return true;
    }

    private static Dictionary<string, object?> Wartosci(EntityEntry wpis, IEnumerable<PropertyEntry> wlasciwosci, bool stare)
    {
        Maskowane.TryGetValue(wpis.Metadata.ClrType, out var maskowane);
        return wlasciwosci.ToDictionary(
            p => p.Metadata.Name,
            p => maskowane?.Contains(p.Metadata.Name) == true ? "[ukryte]" : stare ? p.OriginalValue : p.CurrentValue);
    }
}
