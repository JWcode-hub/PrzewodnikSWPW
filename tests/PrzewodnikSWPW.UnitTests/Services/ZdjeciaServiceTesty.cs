using PrzewodnikSWPW.Web.Services;
using static PrzewodnikSWPW.UnitTests.Services.ObrazyTestowe;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>Reguły wgrywanego pliku (WF-26) — bez bazy i bez HTTP.</summary>
public class ZdjeciaServiceTesty
{
    private const long Limit = 3 * 1024 * 1024;

    private static WgrywanyPlik Plik(string nazwa, string mime, byte[] dane) => new(nazwa, mime, dane);

    [Theory]
    [InlineData("korytarz.jpg", "image/jpeg")]
    [InlineData("KORYTARZ.JPEG", "image/jpeg")]
    [InlineData("korytarz.jpg", "IMAGE/JPEG")]
    public void PoprawnyJpeg_Przyjety(string nazwa, string mime)
    {
        var (obraz, blad) = ZdjeciaService.SprawdzPlik(Plik(nazwa, mime, Jpeg(640, 480)), Limit);

        Assert.Null(blad);
        Assert.Equal(FormatObrazu.Jpeg, obraz!.Format);
    }

    [Fact]
    public void PoprawnePngIWebP_Przyjete()
    {
        Assert.Null(ZdjeciaService.SprawdzPlik(Plik("a.png", "image/png", Png(10, 10)), Limit).Blad);
        Assert.Null(ZdjeciaService.SprawdzPlik(Plik("a.webp", "image/webp", WebPVp8L(10, 10)), Limit).Blad);
    }

    [Fact]
    public void BrakPliku_Blad()
    {
        Assert.Equal("Wybierz plik zdjęcia.", ZdjeciaService.SprawdzPlik(null, Limit).Blad);
        Assert.Equal("Wybierz plik zdjęcia.", ZdjeciaService.SprawdzPlik(Plik("a.jpg", "image/jpeg", []), Limit).Blad);
    }

    [Fact]
    public void PonadLimit_BladZRozmiaremIWskazowka()
    {
        var blad = ZdjeciaService.SprawdzPlik(Plik("a.jpg", "image/jpeg", new byte[Limit + 1]), Limit).Blad;

        Assert.StartsWith("Plik ma 3,0 MB, a limit to 3 MB.", blad);
        Assert.Contains("1600 pikseli", blad);
    }

    [Theory]
    [InlineData("zdjecie.gif", "image/gif")]
    [InlineData("zdjecie.svg", "image/svg+xml")]  // SVG może zawierać skrypt
    [InlineData("zdjecie.exe", "application/octet-stream")]
    [InlineData("zdjecie", "image/jpeg")]
    [InlineData("zdjecie.jpg.exe", "image/jpeg")]
    public void RozszerzeniePozaBialaLista_Odrzucone(string nazwa, string mime)
    {
        Assert.Contains("nie są przyjmowane", ZdjeciaService.SprawdzPlik(Plik(nazwa, mime, Jpeg(10, 10)), Limit).Blad);
    }

    [Theory]
    [InlineData("image/png")]        // rozszerzenie .jpg, typ PNG
    [InlineData("text/html")]
    [InlineData("")]
    public void TypMimeNiezgodnyLubSpozaListy_Odrzucony(string mime)
    {
        Assert.StartsWith("Typ pliku nie zgadza się", ZdjeciaService.SprawdzPlik(Plik("a.jpg", mime, Jpeg(10, 10)), Limit).Blad);
    }

    [Fact]
    public void StronaHtmlZRozszerzeniemJpg_Odrzucona()
    {
        var html = System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");

        Assert.StartsWith("Plik nie jest poprawnym zdjęciem", ZdjeciaService.SprawdzPlik(Plik("a.jpg", "image/jpeg", html), Limit).Blad);
    }

    [Fact]
    public void PngZRozszerzeniemJpg_Odrzucony()
    {
        var blad = ZdjeciaService.SprawdzPlik(Plik("a.jpg", "image/jpeg", Png(10, 10)), Limit).Blad;

        Assert.Equal("Rozszerzenie .jpg nie zgadza się z zawartością pliku (to obraz PNG). Zapisz zdjęcie ponownie w programie graficznym.", blad);
    }

}
