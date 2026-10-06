namespace PrzewodnikSWPW.Web.Services;

public enum StatusZgodnosci { Zgodna, CzesciowoZgodna, Niezgodna }

/// <summary>
/// Dane Deklaracji Dostępności (WF-31, art. 10 ustawy, Zarządzenie 14/2021 §3) — w konfiguracji, nie w widoku,
/// bo zmieniają się przy każdym przeglądzie (art. 11) i po wdrożeniu (sprawa O-02). Puste dane kontaktowe
/// strona oznacza jako do uzupełnienia, zamiast wymyślać adres.
/// </summary>
public sealed class DeklaracjaDostepnosciOptions
{
    public const string Sekcja = "Przewodnik:Deklaracja";

    public string NazwaPodmiotu { get; set; } = "Szkoła Wyższa im. Pawła Włodkowica w Płocku";

    /// <summary>Adres serwisu; gdy pusty — adres, pod którym strona została otwarta.</summary>
    public string? AdresSerwisu { get; set; }

    public DateOnly DataPublikacji { get; set; }
    public DateOnly DataAktualizacji { get; set; }
    public DateOnly DataSporzadzenia { get; set; }

    public StatusZgodnosci Status { get; set; } = StatusZgodnosci.CzesciowoZgodna;

    /// <summary>Znane niezgodności — wymagane przy statusie innym niż „zgodna”.</summary>
    public string[] Niezgodnosci { get; set; } = [];

    public string SposobOceny { get; set; } = "samoocena";
    public string? Audytor { get; set; }

    public string? OsobaKontaktowa { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }

    /// <summary>Informacja o tłumaczu polskiego języka migowego (art. 10 ust. 4 pkt 7).</summary>
    public string? TlumaczMigowy { get; set; }

    public string OpisStatusu => Status switch
    {
        StatusZgodnosci.Zgodna => "w pełni zgodna",
        StatusZgodnosci.CzesciowoZgodna => "częściowo zgodna",
        _ => "niezgodna",
    };
}

/// <summary>Jedno źródło wykazu skrótów (D-02) — strona /skroty-klawiszowe i Deklaracja Dostępności korzystają z tej samej listy.</summary>
public static class SkrotyKlawiszowe
{
    public sealed record Skrot(string Klawisze, string Dzialanie, string GdzieDziala);

    public static readonly IReadOnlyList<Skrot> Wszystkie =
    [
        new("Alt + 1", "Idź prosto", "ekran miejsca w spacerze"),
        new("Alt + 2", "Skręć w lewo", "ekran miejsca w spacerze"),
        new("Alt + 3", "Skręć w prawo", "ekran miejsca w spacerze"),
        new("Alt + 4", "Zawróć", "ekran miejsca w spacerze"),
        new("Alt + P", "Powtórz opis miejsca (odczytuje go czytnik ekranu, a po włączeniu mowy w ustawieniach — głos przeglądarki)", "ekran miejsca w spacerze"),
    ];
}
