namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Alternatywny sposób dostępu, gdy przewodnik nie może wyznaczyć trasy — obowiązek z art. 7
/// ustawy o dostępności cyfrowej (WN-37). Dane kontaktowe podaje dział IT Uczelni (sprawa O-02
/// w docs/09_DECYZJE.md) — nie wpisujemy ich w kod.
/// </summary>
public sealed class KontaktAlternatywnyOptions
{
    public const string Sekcja = "Przewodnik:KontaktAlternatywny";

    /// <summary>Gdzie osobiście uzyskać pomoc — działa nawet bez telefonu.</summary>
    public string Opis { get; set; } = "Zapytaj w portierni w holu głównym budynku A.";

    /// <summary>Numer telefonu portierni w zapisie do wyświetlenia, np. „24 000 00 00”.</summary>
    public string? Telefon { get; set; }

    public string? Email { get; set; }

    /// <summary>Numer do atrybutu href="tel:" — same cyfry i ewentualny „+”.</summary>
    public string? TelefonHref =>
        string.IsNullOrWhiteSpace(Telefon) ? null : new string(Telefon.Where(z => char.IsDigit(z) || z == '+').ToArray());
}
