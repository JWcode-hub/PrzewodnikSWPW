using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>Miejsce wiersza na osi trasy — widok wyróżnia początek i cel.</summary>
public enum RolaKroku
{
    Posredni = 0,
    Poczatek = 1,
    Cel = 2,
}

/// <summary>
/// Jeden wiersz osi trasy. Pierwszy wiersz to punkt startowy (bez odległości i rodzaju przejścia);
/// każdy następny opisuje jedną krawędź i nosi nazwę miejsca, do którego ta krawędź prowadzi.
/// </summary>
/// <param name="Tekst">Instrukcja słowna, zaczyna się od czasownika (D-03).</param>
/// <param name="NazwaPunktu">Punkt startowy albo cel krawędzi: nazwa punktu ruchu lub „Sala A15 — nazwa”.</param>
/// <param name="Metry">Długość krawędzi; <c>null</c> w wierszu początkowym.</param>
/// <param name="Rodzaj">Rodzaj przejścia krawędzi; <c>null</c> w wierszu początkowym.</param>
public sealed record Instrukcja(
    string Tekst,
    bool CzySchody,
    string NazwaPunktu,
    decimal? Metry,
    RodzajPrzejscia? Rodzaj,
    RolaKroku Rola);

/// <summary>
/// Zamienia ścieżkę krawędzi na instrukcje słowne (04_BAZA_DANYCH.md rozdz. 8): porównuje azymut
/// każdej krawędzi ze zwrotem użytkownika — przeliczenie wyłącznie przez <see cref="Azymuty"/>.
/// Każda instrukcja zaczyna się od czasownika czynnościowego (decyzja D-03).
/// Dla n krawędzi zwraca n+1 wierszy: punkt startowy i po jednym na krawędź.
/// </summary>
public sealed class GeneratorOpisuService
{
    /// <param name="zwrotPoczatkowy">Zwrot użytkownika na początku pierwszej krawędzi.</param>
    /// <param name="wstep">Instrukcja wiersza początkowego, np. „Wyjdź z sali A14 na korytarz.” Bez niej — „Zacznij w miejscu: …”.</param>
    /// <param name="punktStartowy">Punkt, w którym trasa się zaczyna; domyślnie początek pierwszej krawędzi.</param>
    public IReadOnlyList<Instrukcja> Opisz(
        GrafBudynku graf, IReadOnlyList<KrawedzGrafu> sciezka, int zwrotPoczatkowy, string? wstep = null, int? punktStartowy = null)
    {
        var start = punktStartowy ?? (sciezka.Count > 0 ? sciezka[0].Z : (int?)null);
        var nazwaStartu = start is int id && graf.Wezly.TryGetValue(id, out var wezelStartu) ? wezelStartu.Nazwa : "Początek trasy";

        var instrukcje = new List<Instrukcja>(sciezka.Count + 1)
        {
            new(wstep ?? $"Zacznij w miejscu: {nazwaStartu}.", false, nazwaStartu, null, null, RolaKroku.Poczatek),
        };

        var zwrot = zwrotPoczatkowy;
        for (var i = 0; i < sciezka.Count; i++)
        {
            var krawedz = sciezka[i];
            var kierunek = Azymuty.NaWzgledny(krawedz.Azymut, zwrot);
            var tekst = Poczatek(kierunek, krawedz.Waga);

            if (krawedz.CzySchody)
            {
                tekst += krawedz.Rodzaj == RodzajPrzejscia.Schody
                    ? " Uwaga — na tym odcinku są schody."
                    : " Uwaga — ten odcinek nie jest dostępny bez schodów.";
            }
            else if (krawedz.Rodzaj == RodzajPrzejscia.Winda)
            {
                tekst += " Skorzystaj z windy.";
            }

            if (!string.IsNullOrWhiteSpace(krawedz.OpisPrzejscia))
            {
                tekst += " " + krawedz.OpisPrzejscia.Trim();
            }

            var nazwaCelu = krawedz.DoPunktu is int punktId && graf.Wezly.TryGetValue(punktId, out var wezel) ? wezel.Nazwa : "Dalszy odcinek trasy";
            if (krawedz.DoSali is int salaId && graf.Sale.TryGetValue(salaId, out var sala))
            {
                tekst += $" Wejdź do sali {sala.Symbol} — {sala.Nazwa}.";
                nazwaCelu = $"Sala {sala.Symbol} — {sala.Nazwa}";
            }

            var rola = i == sciezka.Count - 1 ? RolaKroku.Cel : RolaKroku.Posredni;
            instrukcje.Add(new Instrukcja(tekst, krawedz.CzySchody, nazwaCelu, krawedz.Waga, krawedz.Rodzaj, rola));
            zwrot = krawedz.Azymut; // po przejściu krawędzią zwrot = jej azymut
        }

        return instrukcje;
    }

    /// <summary>Różnica azymutów: 0 → „Idź prosto”, +90 → „Skręć w prawo”, −90 → „Skręć w lewo”, 180 → „Zawróć”.</summary>
    internal static string Poczatek(KierunekWzgledny kierunek, decimal metry)
    {
        var odleglosc = NawigacjaService.Metry(metry);
        return kierunek switch
        {
            KierunekWzgledny.Prosto => $"Idź prosto {odleglosc}.",
            KierunekWzgledny.WPrawo => $"Skręć w prawo i idź {odleglosc}.",
            KierunekWzgledny.WLewo => $"Skręć w lewo i idź {odleglosc}.",
            KierunekWzgledny.DoTylu => $"Zawróć i idź {odleglosc}.",
            _ => throw new ArgumentOutOfRangeException(nameof(kierunek), kierunek, null),
        };
    }
}
