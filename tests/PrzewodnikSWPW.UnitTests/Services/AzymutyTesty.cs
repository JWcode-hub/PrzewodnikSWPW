using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>
/// Przeliczanie azymutów — ryzyko P-01, najpoważniejsze ryzyko funkcjonalne projektu.
/// Oczekiwane wartości wpisane ręcznie (a nie wyliczone tym samym wzorem), aby test
/// sprawdzał znaczenie, a nie powtarzał implementację.
/// </summary>
public class AzymutyTesty
{
    public static readonly int[] Kierunki4 = [0, 90, 180, 270];

    public static TheoryData<int, int> WszystkiePary()
    {
        var dane = new TheoryData<int, int>();
        foreach (var azymut in Kierunki4)
        {
            foreach (var zwrot in Kierunki4)
            {
                dane.Add(azymut, zwrot);
            }
        }
        return dane;
    }

    [Theory]
    // zwrot 0 (twarzą na północ budynku)
    [InlineData(0, 0, KierunekWzgledny.Prosto)]
    [InlineData(90, 0, KierunekWzgledny.WPrawo)]
    [InlineData(180, 0, KierunekWzgledny.DoTylu)]
    [InlineData(270, 0, KierunekWzgledny.WLewo)]
    // zwrot 90 (twarzą na wschód) — północ jest po lewej
    [InlineData(0, 90, KierunekWzgledny.WLewo)]
    [InlineData(90, 90, KierunekWzgledny.Prosto)]
    [InlineData(180, 90, KierunekWzgledny.WPrawo)]
    [InlineData(270, 90, KierunekWzgledny.DoTylu)]
    // zwrot 180 (twarzą na południe) — wszystko odwrócone
    [InlineData(0, 180, KierunekWzgledny.DoTylu)]
    [InlineData(90, 180, KierunekWzgledny.WLewo)]
    [InlineData(180, 180, KierunekWzgledny.Prosto)]
    [InlineData(270, 180, KierunekWzgledny.WPrawo)]
    // zwrot 270 (twarzą na zachód) — północ jest po prawej
    [InlineData(0, 270, KierunekWzgledny.WPrawo)]
    [InlineData(90, 270, KierunekWzgledny.DoTylu)]
    [InlineData(180, 270, KierunekWzgledny.WLewo)]
    [InlineData(270, 270, KierunekWzgledny.Prosto)]
    public void NaWzgledny_Wszystkie16Kombinacji(int azymutKrawedzi, int zwrot, KierunekWzgledny oczekiwany)
    {
        Assert.Equal(oczekiwany, Azymuty.NaWzgledny(azymutKrawedzi, zwrot));
    }

    [Theory]
    [MemberData(nameof(WszystkiePary))]
    public void NaBezwzgledny_JestOdwrotnosciaNaWzgledny(int azymut, int zwrot)
    {
        Assert.Equal(azymut, Azymuty.NaBezwzgledny(Azymuty.NaWzgledny(azymut, zwrot), zwrot));
    }

    [Theory]
    [MemberData(nameof(WszystkiePary))]
    public void NaWzgledny_JestOdwrotnosciaNaBezwzgledny(int przesuniecie, int zwrot)
    {
        var wzgledny = (KierunekWzgledny)przesuniecie;
        Assert.Equal(wzgledny, Azymuty.NaWzgledny(Azymuty.NaBezwzgledny(wzgledny, zwrot), zwrot));
    }

    [Theory]
    [InlineData(0, -90, KierunekWzgledny.WPrawo)]    // zwrot −90 ≡ 270
    [InlineData(-90, 0, KierunekWzgledny.WLewo)]     // azymut −90 ≡ 270
    [InlineData(0, -270, KierunekWzgledny.WLewo)]    // zwrot −270 ≡ 90
    [InlineData(450, 0, KierunekWzgledny.WPrawo)]    // 450 ≡ 90
    [InlineData(0, 720, KierunekWzgledny.Prosto)]
    [InlineData(-360, -720, KierunekWzgledny.Prosto)]
    public void NaWzgledny_ObslugujeWartosciUjemneIPowyzej360(int azymut, int zwrot, KierunekWzgledny oczekiwany)
    {
        Assert.Equal(oczekiwany, Azymuty.NaWzgledny(azymut, zwrot));
    }

    [Theory]
    [InlineData(KierunekWzgledny.WLewo, -90, 180)]
    [InlineData(KierunekWzgledny.Prosto, -90, 270)]
    [InlineData(KierunekWzgledny.DoTylu, -180, 0)]
    [InlineData(KierunekWzgledny.WPrawo, 630, 0)]    // 630 ≡ 270, 270 + 90 = 360 ≡ 0
    public void NaBezwzgledny_ObslugujeWartosciUjemneIPowyzej360(KierunekWzgledny wzgledny, int zwrot, int oczekiwany)
    {
        Assert.Equal(oczekiwany, Azymuty.NaBezwzgledny(wzgledny, zwrot));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(-10)]
    [InlineData(100)]
    public void Normalizuj_OdrzucaKatNiebedacyWielokrotnoscia90(int stopnie)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Azymuty.Normalizuj(stopnie));
    }

    [Theory]
    [InlineData(KierunekWzgledny.Prosto, "Idź prosto")]
    [InlineData(KierunekWzgledny.WPrawo, "Skręć w prawo")]
    [InlineData(KierunekWzgledny.WLewo, "Skręć w lewo")]
    [InlineData(KierunekWzgledny.DoTylu, "Zawróć")]
    public void Etykieta_ZaczynaSieOdCzasownikaCzynnosciowego(KierunekWzgledny k, string oczekiwana)
    {
        Assert.Equal(oczekiwana, Azymuty.Etykieta(k));
    }

    [Fact]
    public void KolejnoscNaLiscie_ZgodnaZDecyzjaD01()
    {
        Assert.Equal(
            [KierunekWzgledny.Prosto, KierunekWzgledny.WLewo, KierunekWzgledny.WPrawo, KierunekWzgledny.DoTylu],
            Azymuty.KolejnoscNaLiscie);
    }

    [Theory]
    [InlineData(1, "1 metr")]
    [InlineData(2, "2 metry")]
    [InlineData(4, "4 metry")]
    [InlineData(5, "5 metrów")]
    [InlineData(12, "12 metrów")]
    [InlineData(14, "14 metrów")]
    [InlineData(22, "22 metry")]
    [InlineData(25, "25 metrów")]
    [InlineData(0.5, "0,5 metra")]
    public void Metry_OdmieniaPoPolsku(double metry, string oczekiwany)
    {
        Assert.Equal(oczekiwany, NawigacjaService.Metry((decimal)metry));
    }
}
