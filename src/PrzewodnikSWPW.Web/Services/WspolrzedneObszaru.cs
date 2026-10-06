using System.Globalization;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Współrzędne aktywnego obszaru w formacie atrybutu <c>coords</c> elementu &lt;area&gt;, w pikselach obrazu
/// Szerokosc × Wysokosc (D-09 pkt 5, D-10 pkt 4). Redaktor wpisuje je z klawiatury albo zaznacza myszą (WF-27).
/// </summary>
public static class WspolrzedneObszaru
{
    public static readonly IReadOnlyDictionary<string, string> Ksztalty = new Dictionary<string, string>
    {
        ["rect"] = "prostokąt",
        ["circle"] = "koło",
        ["poly"] = "wielokąt",
    };

    /// <summary>
    /// Sprawdza i normalizuje („10, 20 ,30,40” → „10,20,30,40”). Zwraca (współrzędne, null) albo (null, komunikat błędu).
    /// Gdy wymiary zdjęcia są nieznane, sprawdzany jest tylko format.
    /// </summary>
    public static (string? Wspolrzedne, string? Blad) Sprawdz(string ksztalt, string? tekst, int? szerokosc, int? wysokosc)
    {
        var czesci = (tekst ?? "").Split(',', StringSplitOptions.TrimEntries);
        var liczby = new List<int>(czesci.Length);
        foreach (var c in czesci)
        {
            if (!int.TryParse(c, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                return (null, "Wpisz liczby całkowite (piksele) oddzielone przecinkami, np. 120,80,360,400.");
            }
            liczby.Add(n);
        }

        var blad = ksztalt switch
        {
            "rect" when liczby.Count != 4 => "Prostokąt wymaga czterech liczb: x1,y1,x2,y2 — lewy górny i prawy dolny róg.",
            "rect" when liczby[0] >= liczby[2] || liczby[1] >= liczby[3] => "Prawy dolny róg (x2,y2) musi leżeć na prawo i niżej niż lewy górny (x1,y1).",
            "circle" when liczby.Count != 3 => "Koło wymaga trzech liczb: x,y środka i promień r.",
            "circle" when liczby[2] == 0 => "Promień koła musi być większy od zera.",
            "poly" when liczby.Count < 6 || liczby.Count % 2 != 0 => "Wielokąt wymaga co najmniej trzech punktów: x1,y1,x2,y2,x3,y3…",
            "rect" or "circle" or "poly" => null,
            _ => "Wybierz kształt obszaru z listy.",
        };
        if (blad is null && szerokosc is int w && wysokosc is int h)
        {
            blad = PozaZdjeciem(ksztalt, liczby, w, h);
        }
        return blad is null ? (string.Join(',', liczby), null) : (null, blad);
    }

    private static string? PozaZdjeciem(string ksztalt, List<int> l, int w, int h)
    {
        var poza = ksztalt == "circle"
            ? l[0] > w || l[1] > h
            : l.Where((_, i) => i % 2 == 0).Any(x => x > w) || l.Where((_, i) => i % 2 == 1).Any(y => y > h);
        return poza ? $"Współrzędne wychodzą poza zdjęcie — ma ono {w} × {h} pikseli." : null;
    }
}
