using System.Buffers.Binary;
using System.Text;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>
/// Minimalne pliki obrazów składane bajt po bajcie — tylko struktura, którą czyta <c>Obrazy</c>
/// (segmenty JPEG, fragmenty PNG i RIFF). Metadane zawierają znacznik <see cref="Tajne"/>,
/// po którym testy sprawdzają, że zniknęły z zapisanego pliku.
/// </summary>
public static class ObrazyTestowe
{
    public const string Tajne = "GPS-52.5468N-19.7064E";
    public const string Komentarz = "komentarz-aparatu";

    /// <summary>Dane obrazu po SOS — muszą przetrwać usuwanie metadanych bez zmian.</summary>
    public static readonly byte[] DaneSkanu = [0x12, 0x34, 0x56, 0x78, 0xFF, 0x00, 0x9A];

    public static byte[] Jpeg(int szerokosc, int wysokosc, int orientacja = 1, bool zApp0 = true)
    {
        var s = new MemoryStream();
        s.Write([0xFF, 0xD8]);
        if (zApp0) s.Write([0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);

        // APP1 EXIF (little-endian): IFD0 z Orientation, a za nim „dane GPS”.
        var tiff = new MemoryStream();
        tiff.Write("II*\0"u8);
        tiff.Write(Le32(8));
        tiff.Write(Le16(1));
        tiff.Write([0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, (byte)orientacja, 0x00, 0x00, 0x00]);
        tiff.Write(Le32(0));
        tiff.Write(Encoding.ASCII.GetBytes(Tajne));
        Segment(s, 0xE1, [.. "Exif\0\0"u8, .. tiff.ToArray()]);

        Segment(s, 0xE2, [.. "ICC_PROFILE\0"u8, 1, 1, 0xAB]);          // profil kolorów — zostaje
        Segment(s, 0xED, [.. "Photoshop 3.0\0"u8, .. Encoding.ASCII.GetBytes(Tajne)]); // IPTC — znika
        Segment(s, 0xFE, Encoding.ASCII.GetBytes(Komentarz));           // COM — znika
        Segment(s, 0xDB, new byte[65]);                                 // DQT — zostaje
        Segment(s, 0xC0, [8, .. Be16(wysokosc), .. Be16(szerokosc), 1, 1, 0x11, 0]);
        s.Write([0xFF, 0xDA, 0x00, 0x08, 1, 1, 0x00, 0x00, 0x3F, 0x00]);
        s.Write(DaneSkanu);
        s.Write([0xFF, 0xD9]);
        return s.ToArray();
    }

    public static byte[] Png(int szerokosc, int wysokosc)
    {
        var s = new MemoryStream();
        s.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Fragment(s, "IHDR", [.. Be32(szerokosc), .. Be32(wysokosc), 8, 2, 0, 0, 0]);
        Fragment(s, "tEXt", [.. "Comment\0"u8, .. Encoding.ASCII.GetBytes(Tajne)]);
        Fragment(s, "eXIf", Encoding.ASCII.GetBytes(Tajne));
        Fragment(s, "IDAT", [0x78, 0x9C, 0x01]);
        Fragment(s, "IEND", []);
        return s.ToArray();
    }

    /// <summary>WebP z rozszerzonym nagłówkiem VP8X (flagi EXIF i XMP ustawione) i fragmentami metadanych.</summary>
    public static byte[] WebPVp8X(int szerokosc, int wysokosc)
    {
        var tresc = new MemoryStream();
        tresc.Write("WEBP"u8);
        FragmentRiff(tresc, "VP8X", [0x0C, 0, 0, 0, .. Le24(szerokosc - 1), .. Le24(wysokosc - 1)]);
        FragmentRiff(tresc, "VP8L", [0x2F, 0, 0, 0, 0, 0x10]);
        FragmentRiff(tresc, "EXIF", Encoding.ASCII.GetBytes(Tajne));
        FragmentRiff(tresc, "XMP ", Encoding.ASCII.GetBytes("<x:xmpmeta>" + Tajne));
        return [.. "RIFF"u8, .. Le32((int)tresc.Length), .. tresc.ToArray()];
    }

    /// <summary>WebP bezstratny: 14 bitów szerokości−1 i 14 bitów wysokości−1 po bajcie sygnatury 0x2F.</summary>
    public static byte[] WebPVp8L(int szerokosc, int wysokosc)
    {
        var bity = (uint)(szerokosc - 1) | (uint)(wysokosc - 1) << 14;
        var dane = new byte[] { 0x2F, 0, 0, 0, 0 };
        BinaryPrimitives.WriteUInt32LittleEndian(dane.AsSpan(1), bity);
        var tresc = new MemoryStream();
        tresc.Write("WEBP"u8);
        FragmentRiff(tresc, "VP8L", dane);
        return [.. "RIFF"u8, .. Le32((int)tresc.Length), .. tresc.ToArray()];
    }

    /// <summary>WebP stratny: nagłówek ramki (3 bajty), kod startu 9D 01 2A, 14-bitowe wymiary.</summary>
    public static byte[] WebPVp8(int szerokosc, int wysokosc)
    {
        var tresc = new MemoryStream();
        tresc.Write("WEBP"u8);
        FragmentRiff(tresc, "VP8 ", [0x10, 0x02, 0x00, 0x9D, 0x01, 0x2A, .. Le16(szerokosc), .. Le16(wysokosc), 0, 0]);
        return [.. "RIFF"u8, .. Le32((int)tresc.Length), .. tresc.ToArray()];
    }

    private static void Segment(Stream s, byte znacznik, byte[] tresc)
    {
        s.Write([0xFF, znacznik]);
        s.Write(Be16(tresc.Length + 2));
        s.Write(tresc);
    }

    private static void Fragment(Stream s, string typ, byte[] dane)
    {
        s.Write(Be32(dane.Length));
        s.Write(Encoding.ASCII.GetBytes(typ));
        s.Write(dane);
        s.Write([0, 0, 0, 0]); // CRC — Obrazy go nie sprawdza
    }

    private static void FragmentRiff(Stream s, string typ, byte[] dane)
    {
        s.Write(Encoding.ASCII.GetBytes(typ));
        s.Write(Le32(dane.Length));
        s.Write(dane);
        if (dane.Length % 2 == 1) s.WriteByte(0);
    }

    private static byte[] Be16(int v) => [(byte)(v >> 8), (byte)v];
    private static byte[] Be32(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
    private static byte[] Le16(int v) => [(byte)v, (byte)(v >> 8)];
    private static byte[] Le24(int v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16)];
    private static byte[] Le32(int v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];

    public static bool Zawiera(byte[] dane, string tekst) =>
        dane.AsSpan().IndexOf(Encoding.ASCII.GetBytes(tekst)) >= 0;
}
