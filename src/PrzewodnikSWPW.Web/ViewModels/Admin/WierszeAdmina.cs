using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.ViewModels.Admin;

// Wiersze tabel panelu — widoki nie dostają encji (CLAUDE.md zasada 11), także na listach tylko do odczytu.

public sealed record BudynekWiersz(int Id, string Kod, string Nazwa, int LiczbaPieter, bool CzyAktywny)
{
    public static BudynekWiersz Z(Budynek b) => new(b.Id, b.Kod, b.Nazwa, b.Pietra.Count, b.CzyAktywny);
}

public sealed record PietroWiersz(int Id, string KodBudynku, int Numer, string Nazwa, int LiczbaSal, int LiczbaPunktow)
{
    public static PietroWiersz Z(Pietro p) => new(p.Id, p.Budynek.Kod, p.Numer, p.Nazwa, p.Sale.Count, p.PunktyRuchu.Count);
}

public sealed record SalaWiersz(int Id, string Symbol, string Nazwa, string Pietro, string Typ, string? PunktWejsciowy, bool CzyAktywna)
{
    public static SalaWiersz Z(Sala s) =>
        new(s.Id, s.Symbol, s.Nazwa, $"{s.Pietro.Budynek.Kod}: {s.Pietro.Nazwa}", s.TypSali.Nazwa, s.PunktWejsciowy?.Kod, s.CzyAktywna);
}

public sealed record PunktWiersz(int Id, string Kod, string Nazwa, string Pietro, string Typ, int LiczbaAktywnychKierunkow, bool CzyAktywny)
{
    public static PunktWiersz Z(PunktRuchu p) =>
        new(p.Id, p.Kod, p.Nazwa, $"{p.Pietro.Budynek.Kod}: {p.Pietro.Nazwa}", p.TypPunktu?.Nazwa ?? "",
            p.KierunkiWychodzace.Count(k => k.CzyAktywny), p.CzyAktywny);
}

public sealed record KierunekWiersz(int Id, int Azymut, string Cel, decimal Waga, string Rodzaj, bool CzyAktywny, bool MaPowrotny, string? Opis)
{
    public static KierunekWiersz Z(Kierunek k) => new(
        k.Id, k.Azymut,
        k.PunktDocelowy is { } p ? $"punkt {p.Kod} — {p.Nazwa}" : k.SalaDocelowa is { } s ? $"sala {s.Symbol} — {s.Nazwa}" : "—",
        k.Waga, k.RodzajPrzejscia.ToString().ToLowerInvariant(), k.CzyAktywny, k.KierunekPowrotnyId is not null, k.OpisPrzejscia);
}

public sealed record KierunkiPunktuViewModel(PunktWiersz Punkt, IReadOnlyDictionary<int, KierunekWiersz> WedlugAzymutu);

public sealed record SlownikWiersz(int Id, string Nazwa, string? Opis, string? Ikona);

public sealed record SlownikListaViewModel(string Tytul, string NazwaPozycji, bool MaIkone, IReadOnlyList<SlownikWiersz> Pozycje);
