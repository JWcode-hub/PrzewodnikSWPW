using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed record BudynkiViewModel(IReadOnlyList<BudynekNaLiscie> Budynki);

public sealed record BudynekViewModel(
    WidokBudynku Budynek,
    string? AdresStartu,
    IReadOnlyDictionary<int, IReadOnlyList<(PunktNaLiscie Punkt, string Adres)>> PunktyNaPietrach);

/// <summary>
/// Pozycja listy kierunków. <see cref="Href"/> = <c>null</c> oznacza brak przejścia — widok renderuje
/// wtedy zwykły tekst (&lt;span&gt;), nie wyłączony przycisk (06 §2.2).
/// </summary>
public sealed record KierunekNaEkranie(string Parametr, string Tekst, string? Href);

public sealed record MiejsceViewModel(
    WidokMiejsca Miejsce,
    IReadOnlyList<KierunekNaEkranie> Kierunki,
    string? Komunikat,
    string AdresBudynku);

public sealed record SalaViewModel(WidokSali Sala, string? AdresPowrotu);
