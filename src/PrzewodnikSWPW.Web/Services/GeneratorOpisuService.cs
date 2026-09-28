using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

public sealed record Instrukcja(string Tekst, bool CzySchody);

/// <summary>
/// Zamienia ścieżkę krawędzi na instrukcje słowne (04_BAZA_DANYCH.md rozdz. 8): porównuje azymut
/// każdej krawędzi ze zwrotem użytkownika — przeliczenie wyłącznie przez <see cref="Azymuty"/>.
/// Każda instrukcja zaczyna się od czasownika czynnościowego (decyzja D-03).
/// </summary>
public sealed class GeneratorOpisuService
{
    /// <param name="zwrotPoczatkowy">Zwrot użytkownika na początku pierwszej krawędzi.</param>
    /// <param name="wstep">Opcjonalna pierwsza instrukcja, np. „Wyjdź z sali A14 na korytarz.”</param>
    public IReadOnlyList<Instrukcja> Opisz(GrafBudynku graf, IReadOnlyList<KrawedzGrafu> sciezka, int zwrotPoczatkowy, string? wstep = null)
    {
        var instrukcje = new List<Instrukcja>(sciezka.Count + 1);
        if (wstep is not null)
        {
            instrukcje.Add(new Instrukcja(wstep, false));
        }

        var zwrot = zwrotPoczatkowy;
        foreach (var krawedz in sciezka)
        {
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

            if (krawedz.DoSali is int salaId && graf.Sale.TryGetValue(salaId, out var sala))
            {
                tekst += $" Wejdź do sali {sala.Symbol} — {sala.Nazwa}.";
            }

            instrukcje.Add(new Instrukcja(tekst, krawedz.CzySchody));
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
