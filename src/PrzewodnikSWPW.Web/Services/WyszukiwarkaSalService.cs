using System.Diagnostics;
using System.Globalization;
using System.Text;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

public sealed record SalaWyszukana(
    int Id,
    string Symbol,
    string Nazwa,
    string TypSali,
    string NazwaPietra,
    int NumerPietra,
    string KodBudynku,
    string NazwaBudynku);

/// <summary>
/// Wynik wyszukiwania. Gdy <see cref="Wyniki"/> jest puste, <see cref="Propozycje"/> zawiera
/// najbliższe dopasowania („Czy chodziło o A15?”, WCAG 3.3.3), a <see cref="Budynki"/> — budynki,
/// do których listy sal można przejść.
/// </summary>
public sealed record WynikWyszukiwania(
    string Fraza,
    IReadOnlyList<SalaWyszukana> Wyniki,
    IReadOnlyList<SalaWyszukana> Propozycje,
    IReadOnlyList<BudynekNaLiscie> Budynki);

/// <summary>
/// Wyszukiwanie sal odporne na warianty zapisu (WF-10, WF-11, ryzyko P-16): „15”, „a 15”, „A-15”,
/// „sala 15” i „dziekanat” trafiają w tę samą salę.
/// </summary>
public sealed class WyszukiwarkaSalService(IWyszukiwarkaRepozytorium repozytorium, ILogger<WyszukiwarkaSalService> log)
{
    public const int MaksDlugoscFrazy = 100; // ZapytanieTrasy.FrazaZ NVARCHAR(100)

    /// <summary>Słowa ogólne pomijane w zapytaniu, gdy obok jest coś konkretnego: „sala 15” → „15”.</summary>
    private static readonly HashSet<string> SlowaOgolne =
        ["sala", "sali", "sale", "pokoj", "pok", "pomieszczenie", "nr", "numer"];

    /// <summary>
    /// Normalizacja stosowana PO OBU STRONACH porównania: małe litery, bez polskich znaków
    /// diakrytycznych, bez spacji, myślników i kropek. „Sekretariat Wydziału” → „sekretariatwydzialu”.
    /// </summary>
    public static string Normalizuj(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(tekst.Length);
        foreach (var znak in tekst.ToLower(CultureInfo.InvariantCulture).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(znak) == UnicodeCategory.NonSpacingMark)
            {
                continue; // znaki diakrytyczne rozłożone przez FormD (ą → a + ogonek)
            }

            switch (znak)
            {
                case 'ł': sb.Append('l'); break; // „ł” nie rozkłada się w FormD — trzeba ręcznie
                case '-' or '‐' or '–' or '—' or '.':
                    break;
                default:
                    if (!char.IsWhiteSpace(znak))
                    {
                        sb.Append(znak);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Normalizacja frazy wpisanej przez użytkownika — dodatkowo bez słów ogólnych („sala”, „pokój”, „nr”).</summary>
    public static string NormalizujFraze(string? fraza)
    {
        if (string.IsNullOrWhiteSpace(fraza))
        {
            return string.Empty;
        }

        var slowa = fraza.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var konkretne = slowa.Where(s => !SlowaOgolne.Contains(Normalizuj(s))).ToArray();
        // „sala” wpisana samodzielnie zostaje — wtedy szukamy właśnie tego słowa.
        return Normalizuj(string.Join(' ', konkretne.Length > 0 ? konkretne : slowa));
    }

    /// <summary>Pełne wyszukiwanie; zapisuje zapytanie w ZapytanieTrasy (FrazaZ, CzySukces) — P-16 pkt 5.</summary>
    public async Task<WynikWyszukiwania> Szukaj(string fraza, CancellationToken ct = default)
    {
        var stoper = Stopwatch.StartNew();
        fraza = fraza.Trim();
        var sale = await repozytorium.PobierzAktywneSaleAsync(ct);
        var wyniki = Dopasuj(sale, NormalizujFraze(fraza), limit: null);

        var propozycje = wyniki.Count == 0 ? Propozycje(sale, NormalizujFraze(fraza)) : [];
        var budynki = wyniki.Count == 0
            ? sale.Select(s => s.Pietro.Budynek).DistinctBy(b => b.Id).OrderBy(b => b.Kod)
                  .Select(b => new BudynekNaLiscie(b.Kod, b.Nazwa, b.Adres)).ToList()
            : [];

        await ZapiszZapytanie(fraza, wyniki.Count > 0, (int)stoper.ElapsedMilliseconds, ct);
        return new WynikWyszukiwania(fraza, wyniki, propozycje, budynki);
    }

    /// <summary>Podpowiedzi dla pola wyszukiwania (combobox). Nie są zapisywane — to nie jest wysłane zapytanie.</summary>
    public async Task<IReadOnlyList<SalaWyszukana>> Podpowiedzi(string fraza, int limit = 8, CancellationToken ct = default)
    {
        var znormalizowana = NormalizujFraze(fraza);
        if (znormalizowana.Length == 0)
        {
            return [];
        }
        return Dopasuj(await repozytorium.PobierzAktywneSaleAsync(ct), znormalizowana, limit);
    }

    /// <summary>Wszystkie aktywne sale budynku, posortowane po piętrze i symbolu; <c>null</c>, gdy budynku nie ma.</summary>
    public async Task<(BudynekNaLiscie Budynek, IReadOnlyList<SalaWyszukana> Sale)?> SaleBudynku(string kodBudynku, CancellationToken ct = default)
    {
        var sale = (await repozytorium.PobierzAktywneSaleAsync(ct))
            .Where(s => string.Equals(s.Pietro.Budynek.Kod, kodBudynku, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (sale.Count == 0)
        {
            return null;
        }

        var b = sale[0].Pietro.Budynek;
        return (new BudynekNaLiscie(b.Kod, b.Nazwa, b.Adres),
            sale.OrderBy(s => s.Pietro.Numer).ThenBy(s => s.Symbol).Select(Na).ToList());
    }

    /// <summary>
    /// Ocena dopasowania — im wyżej, tym bliżej początku listy. 0 = brak dopasowania.
    /// Symbol ma pierwszeństwo przed nazwą, a nazwa przed aliasem.
    /// </summary>
    internal static int Ocena(Sala sala, string fraza)
    {
        var symbol = Normalizuj(sala.Symbol);
        var nazwa = Normalizuj(sala.Nazwa);
        var aliasy = Aliasy(sala);
        var tylkoCyfry = fraza.All(char.IsDigit);

        if (symbol == fraza) return 100;
        // „15” → „a15”, ale nie „a115”: przed numerem nie może stać kolejna cyfra.
        // (symbol jest tu zawsze dłuższy od frazy — równość obsłużyła linia wyżej.)
        if (tylkoCyfry && symbol.EndsWith(fraza, StringComparison.Ordinal)
            && !char.IsDigit(symbol[symbol.Length - fraza.Length - 1])) return 90;
        if (aliasy.Contains(fraza)) return 85;
        if (nazwa == fraza) return 80;
        if (symbol.StartsWith(fraza, StringComparison.Ordinal)) return 70;
        if (nazwa.StartsWith(fraza, StringComparison.Ordinal)) return 60;
        if (symbol.Contains(fraza, StringComparison.Ordinal)) return 50;
        if (aliasy.Any(a => a.StartsWith(fraza, StringComparison.Ordinal))) return 45;
        if (nazwa.Contains(fraza, StringComparison.Ordinal)) return 40;
        if (aliasy.Any(a => a.Contains(fraza, StringComparison.Ordinal))) return 30;
        return 0;
    }

    private static List<SalaWyszukana> Dopasuj(IEnumerable<Sala> sale, string fraza, int? limit)
    {
        if (fraza.Length == 0)
        {
            return [];
        }

        var dopasowane = sale
            .Select(s => (Sala: s, Ocena: Ocena(s, fraza)))
            .Where(x => x.Ocena > 0)
            .OrderByDescending(x => x.Ocena)
            .ThenBy(x => x.Sala.Pietro.Budynek.Kod)
            .ThenBy(x => x.Sala.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => Na(x.Sala));

        return (limit is int l ? dopasowane.Take(l) : dopasowane).ToList();
    }

    /// <summary>
    /// Najbliższe sale według odległości edycyjnej (z przestawieniem sąsiednich znaków: „a51” → „a15”).
    /// Próg rośnie z długością frazy, aby „a99” nie proponowało przypadkowych sal.
    /// </summary>
    private static List<SalaWyszukana> Propozycje(IEnumerable<Sala> sale, string fraza, int limit = 3)
    {
        if (fraza.Length == 0)
        {
            return [];
        }

        var prog = Math.Max(1, fraza.Length / 3);
        return sale
            .Select(s => (Sala: s, Odleglosc: new[] { Normalizuj(s.Symbol), Normalizuj(s.Nazwa) }
                .Concat(Aliasy(s))
                .Min(kandydat => Odleglosc(fraza, kandydat))))
            .Where(x => x.Odleglosc <= prog)
            .OrderBy(x => x.Odleglosc)
            .ThenBy(x => x.Sala.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => Na(x.Sala))
            .ToList();
    }

    private static List<string> Aliasy(Sala sala) =>
        (sala.Aliasy ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalizuj)
            .Where(a => a.Length > 0)
            .ToList();

    /// <summary>Odległość Damerau-Levenshteina (wariant OSA): wstawienie, usunięcie, zamiana, przestawienie sąsiednich.</summary>
    internal static int Odleglosc(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var koszt = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + koszt);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }
        return d[a.Length, b.Length];
    }

    private static SalaWyszukana Na(Sala s) => new(
        s.Id, s.Symbol, s.Nazwa, s.TypSali.Nazwa, s.Pietro.Nazwa, s.Pietro.Numer, s.Pietro.Budynek.Kod, s.Pietro.Budynek.Nazwa);

    /// <summary>Zapis w logu zapytań. Błąd zapisu nie może zepsuć wyszukiwania — tylko go logujemy.</summary>
    private async Task ZapiszZapytanie(string fraza, bool czySukces, int czasMs, CancellationToken ct)
    {
        try
        {
            await repozytorium.ZapiszZapytanieAsync(new ZapytanieTrasy
            {
                FrazaZ = fraza.Length > MaksDlugoscFrazy ? fraza[..MaksDlugoscFrazy] : fraza,
                CzySukces = czySukces,
                CzasMs = czasMs,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Nie udało się zapisać zapytania wyszukiwania w ZapytanieTrasy.");
        }
    }
}
