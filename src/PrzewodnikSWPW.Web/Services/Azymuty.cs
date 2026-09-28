namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Jedyne miejsce przeliczania azymutu bezwzględnego na kierunek względny i odwrotnie
/// (ryzyko P-01: błędne „w lewo” prowadzi osobę niewidomą w ścianę).
/// </summary>
public static class Azymuty
{
    /// <summary>Kolejność kierunków na każdym ekranie — decyzja D-01 w docs/09_DECYZJE.md.</summary>
    public static readonly IReadOnlyList<KierunekWzgledny> KolejnoscNaLiscie =
    [
        KierunekWzgledny.Prosto,
        KierunekWzgledny.WLewo,
        KierunekWzgledny.WPrawo,
        KierunekWzgledny.DoTylu,
    ];

    /// <summary>
    /// kierunekWzgledny = (azymutKrawedzi − zwrotUzytkownika) mod 360.
    /// Przyjmuje także wartości ujemne i ≥ 360 (np. zwrot −90 = 270).
    /// </summary>
    public static KierunekWzgledny NaWzgledny(int azymutKrawedzi, int zwrotUzytkownika) =>
        (KierunekWzgledny)Normalizuj(azymutKrawedzi - zwrotUzytkownika);

    /// <summary>azymut = (przesunięcie kierunku względnego + zwrotUzytkownika) mod 360.</summary>
    public static int NaBezwzgledny(KierunekWzgledny wzgledny, int zwrotUzytkownika) =>
        Normalizuj((int)wzgledny + zwrotUzytkownika);

    /// <summary>
    /// Sprowadza kąt do przedziału [0, 360). W C# operator % zachowuje znak dzielnej
    /// (−90 % 360 = −90), dlatego potrzebne jest dodatkowe „+ 360) % 360”.
    /// </summary>
    public static int Normalizuj(int stopnie)
    {
        var wynik = ((stopnie % 360) + 360) % 360;
        if (wynik % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stopnie), stopnie,
                "Azymut musi być wielokrotnością 90 stopni (0, 90, 180, 270).");
        }
        return wynik;
    }

    /// <summary>Etykieta czynności — metoda proporcjonalna z czasownikiem (decyzja D-03).</summary>
    public static string Etykieta(KierunekWzgledny k) => k switch
    {
        KierunekWzgledny.Prosto => "Idź prosto",
        KierunekWzgledny.WPrawo => "Skręć w prawo",
        KierunekWzgledny.WLewo => "Skręć w lewo",
        KierunekWzgledny.DoTylu => "Zawróć",
        _ => throw new ArgumentOutOfRangeException(nameof(k), k, null),
    };

    /// <summary>Nazwa strony dla kierunku, którym nie da się przejść („W lewo — brak przejścia”).</summary>
    public static string NazwaStrony(KierunekWzgledny k) => k switch
    {
        KierunekWzgledny.Prosto => "Prosto",
        KierunekWzgledny.WPrawo => "W prawo",
        KierunekWzgledny.WLewo => "W lewo",
        KierunekWzgledny.DoTylu => "Do tyłu",
        _ => throw new ArgumentOutOfRangeException(nameof(k), k, null),
    };
}
