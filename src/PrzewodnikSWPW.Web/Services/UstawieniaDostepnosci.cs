namespace PrzewodnikSWPW.Web.Services;

/// <summary>Motywy kolorystyczne — docs/06_DOSTEPNOSC_TTS.md §5.1.</summary>
public enum Motyw
{
    /// <summary>Zgodnie z ustawieniami systemu (prefers-color-scheme, prefers-contrast).</summary>
    Systemowy,
    Podstawowy,
    WysokiKontrast,
    Ciemny,
}

/// <summary>
/// Ustawienia dostępności wybrane przez użytkownika (WF-34). Zapisywane w ciasteczku,
/// aby działały bez JavaScriptu; JavaScript dodatkowo kopiuje je do localStorage.
/// </summary>
public sealed record UstawieniaDostepnosci
{
    public static readonly IReadOnlyList<int> DozwoloneRozmiaryTekstu = [100, 125, 150, 200];
    public static readonly IReadOnlyList<int> DozwoloneTempaMowy = [50, 75, 100, 125, 150, 200];

    public static UstawieniaDostepnosci Domyslne { get; } = new();

    public Motyw Motyw { get; init; } = Motyw.Systemowy;

    /// <summary>Rozmiar tekstu w procentach podstawy (18 px).</summary>
    public int RozmiarTekstu { get; init; } = 100;

    /// <summary>Domyślnie wyłączona — nie może nakładać się na czytnik ekranu (ryzyko P-05).</summary>
    public bool MowaWlaczona { get; init; }

    /// <summary>Tempo mowy w procentach (100 = normalne); Web Speech API dostaje wartość / 100.</summary>
    public int TempoMowy { get; init; } = 100;

    /// <summary>WCAG 2.1.4 — skróty muszą dać się wyłączyć (decyzja D-02).</summary>
    public bool SkrotyWlaczone { get; init; } = true;

    public bool OgraniczAnimacje { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool CzyPoprawne =>
        Enum.IsDefined(Motyw)
        && DozwoloneRozmiaryTekstu.Contains(RozmiarTekstu)
        && DozwoloneTempaMowy.Contains(TempoMowy);
}
