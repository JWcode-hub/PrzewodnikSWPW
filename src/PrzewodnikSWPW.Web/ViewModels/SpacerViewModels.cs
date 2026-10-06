using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed record BudynkiViewModel(IReadOnlyList<BudynekNaLiscie> Budynki);

public sealed record BudynekViewModel(
    WidokBudynku Budynek,
    string? AdresStartu,
    IReadOnlyDictionary<int, IReadOnlyList<(PunktNaLiscie Punkt, string Adres)>> PunktyNaPietrach);

/// <summary>
/// Pozycja listy kierunków. <see cref="Href"/> = <c>null</c> oznacza brak przejścia — widok renderuje
/// wtedy zwykły tekst (&lt;span&gt;), nie wyłączony przycisk (06 §2.2). Na ekranie dwa widoczne wiersze:
/// <see cref="Nazwa"/> (czynność i cel) oraz <see cref="Szczegol"/> (rodzaj przejścia, odległość, opis).
/// </summary>
public sealed record KierunekNaEkranie(string Parametr, string Tekst, string? Href, string Nazwa, string? Szczegol);

public sealed record MiejsceViewModel(
    WidokMiejsca Miejsce,
    IReadOnlyList<KierunekNaEkranie> Kierunki,
    string? Komunikat,
    string AdresBudynku,
    IReadOnlyList<ZdjecieNaEkranie> Zdjecia);

/// <summary>Zdjęcie miejsca z aktywnymi obszarami, które prowadzą tam, gdzie odpowiadające im linki z listy kierunków.</summary>
public sealed record ZdjecieNaEkranie(ZdjecieMiejsca Zdjecie, IReadOnlyList<ObszarNaEkranie> Obszary);

public sealed record ObszarNaEkranie(string Ksztalt, string Wspolrzedne, string Etykieta, string Href);

public sealed record SalaViewModel(WidokSali Sala, string? AdresPowrotu);
