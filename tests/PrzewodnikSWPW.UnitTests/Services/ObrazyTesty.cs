using PrzewodnikSWPW.Web.Services;
using static PrzewodnikSWPW.UnitTests.Services.ObrazyTestowe;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>Rozpoznawanie formatu po zawartości i usuwanie metadanych bez ponownego kodowania (D-10).</summary>
public class ObrazyTesty
{
    [Fact]
    public void Jpeg_RozpoznanyZWymiaramiZSof()
    {
        Assert.Equal(new InformacjaOObrazie(FormatObrazu.Jpeg, 1600, 1200, 1), Obrazy.Rozpoznaj(Jpeg(1600, 1200)));
    }

    [Theory]
    [InlineData(6)] // obrót o 90° — typowe zdjęcie z telefonu trzymanego pionowo
    [InlineData(8)]
    public void JpegZOrientacja5do8_WymiaryWOrientacjiWyswietlanej(int orientacja)
    {
        var obraz = Obrazy.Rozpoznaj(Jpeg(1600, 1200, orientacja));

        Assert.Equal((1200, 1600, orientacja), (obraz!.Szerokosc, obraz.Wysokosc, obraz.OrientacjaExif));
    }

    [Fact]
    public void Png_RozpoznanyZIhdr()
    {
        Assert.Equal(new InformacjaOObrazie(FormatObrazu.Png, 800, 600, 1), Obrazy.Rozpoznaj(Png(800, 600)));
    }

    [Theory]
    [InlineData("VP8X")]
    [InlineData("VP8L")]
    [InlineData("VP8 ")]
    public void WebP_RozpoznanyWeWszystkichWariantachNaglowka(string wariant)
    {
        var dane = wariant switch { "VP8X" => WebPVp8X(1024, 768), "VP8L" => WebPVp8L(1024, 768), _ => WebPVp8(1024, 768) };

        Assert.Equal(new InformacjaOObrazie(FormatObrazu.WebP, 1024, 768, 1), Obrazy.Rozpoznaj(dane));
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")] // strona HTML z rozszerzeniem .jpg
    [InlineData("GIF89a")]
    [InlineData("")]
    public void NieObraz_Odrzucony(string tresc)
    {
        Assert.Null(Obrazy.Rozpoznaj(System.Text.Encoding.UTF8.GetBytes(tresc)));
    }

    [Fact]
    public void UcietyJpegBezSof_Odrzucony()
    {
        Assert.Null(Obrazy.Rozpoznaj(Jpeg(640, 480).AsSpan(0, 40)));
    }

    [Fact]
    public void Jpeg_UsuniecieMetadanych_ZnikajaExifIptcIKomentarz_ZostajaDaneObrazuIProfilKolorow()
    {
        var dane = Jpeg(640, 480);
        var obraz = Obrazy.Rozpoznaj(dane)!;

        var czyste = Obrazy.UsunMetadane(dane, obraz)!;

        Assert.True(Zawiera(dane, Tajne));
        Assert.False(Zawiera(czyste, Tajne));
        Assert.False(Zawiera(czyste, Komentarz));
        Assert.False(Zawiera(czyste, "Exif"));        // orientacja 1 nie potrzebuje segmentu EXIF
        Assert.True(Zawiera(czyste, "JFIF"));
        Assert.True(Zawiera(czyste, "ICC_PROFILE"));
        Assert.Equal([.. DaneSkanu, 0xFF, 0xD9], czyste[^(DaneSkanu.Length + 2)..]);
        Assert.Equal(obraz, Obrazy.Rozpoznaj(czyste)); // plik po zmianie nadal jest tym samym obrazem
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // telefon często zapisuje EXIF jako pierwszy segment, bez JFIF
    public void Jpeg_UsuniecieMetadanych_OrientacjaZachowanaWMinimalnymExif(bool zApp0)
    {
        var dane = Jpeg(1600, 1200, orientacja: 6, zApp0);

        var czyste = Obrazy.UsunMetadane(dane, Obrazy.Rozpoznaj(dane)!)!;

        Assert.False(Zawiera(czyste, Tajne));
        Assert.Equal(new InformacjaOObrazie(FormatObrazu.Jpeg, 1200, 1600, 6), Obrazy.Rozpoznaj(czyste));
        Assert.Equal(zApp0 ? 20 : 2, czyste.AsSpan().IndexOf("Exif"u8) - 4); // po APP0 (JFIF musi być pierwszy) albo zaraz po SOI
    }

    [Fact]
    public void Png_UsuniecieMetadanych_ZnikajaTekstIExif_ZostajaIhdrIdatIend()
    {
        var dane = Png(800, 600);

        var czyste = Obrazy.UsunMetadane(dane, Obrazy.Rozpoznaj(dane)!)!;

        Assert.False(Zawiera(czyste, Tajne));
        Assert.True(Zawiera(czyste, "IDAT"));
        Assert.True(Zawiera(czyste, "IEND"));
        Assert.Equal(new InformacjaOObrazie(FormatObrazu.Png, 800, 600, 1), Obrazy.Rozpoznaj(czyste));
    }

    [Fact]
    public void WebP_UsuniecieMetadanych_ZnikajaExifIXmp_FlagiVp8XWygaszone_RozmiarRiffPoprawny()
    {
        var dane = WebPVp8X(1024, 768);

        var czyste = Obrazy.UsunMetadane(dane, Obrazy.Rozpoznaj(dane)!)!;

        Assert.False(Zawiera(czyste, Tajne));
        Assert.Equal(0, czyste[20] & 0x0C);
        Assert.Equal(czyste.Length - 8, BitConverter.ToInt32(czyste, 4));
        Assert.Equal(new InformacjaOObrazie(FormatObrazu.WebP, 1024, 768, 1), Obrazy.Rozpoznaj(czyste));
    }
}
