using System.Buffers.Binary;
using System.Text;

namespace PrzewodnikSWPW.Web.Services;

public enum FormatObrazu { Jpeg, Png, WebP }

/// <summary>
/// Obraz rozpoznany po zawartości. <see cref="Szerokosc"/> i <see cref="Wysokosc"/> — w orientacji
/// wyświetlanej (dla orientacji EXIF 5–8 zamienione miejscami, D-10 pkt 4).
/// </summary>
public sealed record InformacjaOObrazie(FormatObrazu Format, int Szerokosc, int Wysokosc, int OrientacjaExif)
{
    public string Rozszerzenie => Format switch
    {
        FormatObrazu.Jpeg => "jpg",
        FormatObrazu.Png => "png",
        _ => "webp",
    };
}

/// <summary>
/// Rozpoznawanie obrazów po sygnaturze i nagłówku oraz usuwanie metadanych bez ponownego kodowania (D-10).
/// Działa na strukturze pliku (segmenty JPEG, fragmenty PNG i RIFF) — piksele nie są dekodowane ani zmieniane.
/// Nie ufa nazwie pliku ani typowi MIME od przeglądarki: o formacie decyduje wyłącznie zawartość.
/// </summary>
public static class Obrazy
{
    /// <summary>Większe wymiary traktujemy jak uszkodzony nagłówek, a nie zdjęcie korytarza.</summary>
    public const int MaksWymiar = 20_000;

    private static ReadOnlySpan<byte> SygnaturaPng => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static InformacjaOObrazie? Rozpoznaj(ReadOnlySpan<byte> dane)
    {
        var wynik = dane switch
        {
            [0xFF, 0xD8, 0xFF, ..] => RozpoznajJpeg(dane),
            _ when dane.StartsWith(SygnaturaPng) => RozpoznajPng(dane),
            _ when dane.Length >= 16 && dane[..4].SequenceEqual("RIFF"u8) && dane[8..12].SequenceEqual("WEBP"u8) => RozpoznajWebP(dane),
            _ => null,
        };
        if (wynik is null || wynik.Szerokosc is < 1 or > MaksWymiar || wynik.Wysokosc is < 1 or > MaksWymiar)
        {
            return null;
        }
        return wynik.OrientacjaExif is >= 5 and <= 8 ? wynik with { Szerokosc = wynik.Wysokosc, Wysokosc = wynik.Szerokosc } : wynik;
    }

    /// <summary>Kopia pliku bez metadanych (EXIF, XMP, IPTC, komentarzy). Zwraca <c>null</c> dla uszkodzonej struktury.</summary>
    public static byte[]? UsunMetadane(ReadOnlySpan<byte> dane, InformacjaOObrazie obraz) => obraz.Format switch
    {
        FormatObrazu.Jpeg => UsunMetadaneJpeg(dane, obraz.OrientacjaExif),
        FormatObrazu.Png => UsunMetadanePng(dane),
        _ => UsunMetadaneWebP(dane),
    };

    // --- JPEG --------------------------------------------------------------------------------------------

    /// <summary>
    /// Znaczniki bez długości: SOI, TEM, RST0–7. SOF0–SOF15 (bez DHT, JPG, DAC) niosą wymiary.
    /// Przeglądanie kończy się na SOS — dalej są dane obrazu.
    /// </summary>
    private static bool BezDlugosci(byte znacznik) => znacznik is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7);

    private static bool CzySof(byte znacznik) => znacznik is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    /// <summary>Segmenty zachowywane: wszystkie poza APP1 (EXIF, XMP), APP3–APP13, APP15 i komentarzem (D-10 pkt 2).</summary>
    private static bool CzyMetadane(byte znacznik) => znacznik is 0xE1 or (>= 0xE3 and <= 0xED) or 0xEF or 0xFE;

    private static InformacjaOObrazie? RozpoznajJpeg(ReadOnlySpan<byte> dane)
    {
        int szerokosc = 0, wysokosc = 0, orientacja = 1;
        foreach (var (znacznik, poczatek, dlugosc) in SegmentyJpeg(dane.ToArray()))
        {
            if (znacznik == 0xDA) break;
            var tresc = dane.Slice(poczatek + 4, dlugosc - 2);
            if (CzySof(znacznik) && tresc.Length >= 5)
            {
                wysokosc = BinaryPrimitives.ReadUInt16BigEndian(tresc[1..]);
                szerokosc = BinaryPrimitives.ReadUInt16BigEndian(tresc[3..]);
            }
            else if (znacznik == 0xE1 && tresc.StartsWith("Exif\0\0"u8))
            {
                orientacja = OrientacjaZTiff(tresc[6..]);
            }
        }
        return szerokosc > 0 && wysokosc > 0 ? new InformacjaOObrazie(FormatObrazu.Jpeg, szerokosc, wysokosc, orientacja) : null;
    }

    /// <summary>
    /// Segmenty od SOI do SOS włącznie: (znacznik, pozycja bajtu 0xFF, długość z nagłówka segmentu; 0 dla znaczników bez długości).
    /// Zwraca pustą sekwencję po napotkaniu uszkodzonej struktury — wtedy brak SOF i obraz jest odrzucany.
    /// </summary>
    private static IEnumerable<(byte Znacznik, int Poczatek, int Dlugosc)> SegmentyJpeg(byte[] dane)
    {
        var i = 2;
        while (i + 1 < dane.Length)
        {
            if (dane[i] != 0xFF) yield break;
            var znacznik = dane[i + 1];
            if (znacznik == 0xFF) { i++; continue; } // bajty wypełnienia
            if (BezDlugosci(znacznik)) { i += 2; continue; }
            if (znacznik == 0xD9 || i + 4 > dane.Length) yield break;

            var dlugosc = BinaryPrimitives.ReadUInt16BigEndian(dane.AsSpan(i + 2));
            if (dlugosc < 2 || i + 2 + dlugosc > dane.Length) yield break;
            yield return (znacznik, i, dlugosc);
            if (znacznik == 0xDA) yield break;
            i += 2 + dlugosc;
        }
    }

    private static byte[]? UsunMetadaneJpeg(ReadOnlySpan<byte> dane, int orientacja)
    {
        var tablica = dane.ToArray();
        using var wynik = new MemoryStream(tablica.Length);
        wynik.Write([0xFF, 0xD8]);
        var orientacjaZapisana = orientacja == 1; // orientacja 1 (domyślna) nie potrzebuje segmentu EXIF

        foreach (var (znacznik, poczatek, dlugosc) in SegmentyJpeg(tablica))
        {
            // Minimalny EXIF z samą orientacją — po APP0 (JFIF wymaga pierwszego miejsca), inaczej przed pierwszym segmentem.
            if (!orientacjaZapisana && znacznik != 0xE0)
            {
                wynik.Write(MinimalnyExif(orientacja));
                orientacjaZapisana = true;
            }
            if (znacznik == 0xDA)
            {
                wynik.Write(tablica.AsSpan(poczatek)); // SOS i dane obrazu bez zmian, do końca pliku
                return wynik.ToArray();
            }
            if (!CzyMetadane(znacznik))
            {
                wynik.Write(tablica.AsSpan(poczatek, 2 + dlugosc));
            }
        }
        return null; // brak SOS — plik uszkodzony
    }

    /// <summary>Orientacja (znacznik 0x0112) z IFD0 bloku TIFF w segmencie EXIF; 1, gdy brak albo dane są uszkodzone.</summary>
    private static int OrientacjaZTiff(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8) return 1;
        var le = tiff[..2].SequenceEqual("II"u8);
        if (!le && !tiff[..2].SequenceEqual("MM"u8)) return 1;
        uint U32(ReadOnlySpan<byte> s) => le ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);
        ushort U16(ReadOnlySpan<byte> s) => le ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);

        var ifd = U32(tiff[4..]);
        if (ifd > tiff.Length - 2) return 1;
        var liczba = U16(tiff[(int)ifd..]);
        for (var n = 0; n < liczba; n++)
        {
            var wpis = (int)ifd + 2 + n * 12;
            if (wpis + 12 > tiff.Length) return 1;
            if (U16(tiff[wpis..]) == 0x0112 && U16(tiff[(wpis + 2)..]) == 3)
            {
                var wartosc = U16(tiff[(wpis + 8)..]);
                return wartosc is >= 1 and <= 8 ? wartosc : 1;
            }
        }
        return 1;
    }

    /// <summary>Segment APP1: „Exif\0\0” + TIFF (big-endian) z jednym wpisem IFD0 — Orientation.</summary>
    private static byte[] MinimalnyExif(int orientacja) =>
    [
        0xFF, 0xE1, 0x00, 0x22,
        (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
        (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // nagłówek TIFF, IFD0 od bajtu 8
        0x00, 0x01,                                               // jeden wpis
        0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,           // Orientation, SHORT, 1 wartość
        0x00, (byte)orientacja, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,                                   // brak kolejnego IFD
    ];

    // --- PNG ---------------------------------------------------------------------------------------------

    private static readonly HashSet<string> MetadanePng = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    private static InformacjaOObrazie? RozpoznajPng(ReadOnlySpan<byte> dane)
    {
        // Pierwszy fragment po sygnaturze musi być IHDR o długości 13: szerokość, wysokość (big-endian).
        if (dane.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(dane[8..]) != 13 || !dane[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }
        var szerokosc = BinaryPrimitives.ReadUInt32BigEndian(dane[16..]);
        var wysokosc = BinaryPrimitives.ReadUInt32BigEndian(dane[20..]);
        return szerokosc > MaksWymiar || wysokosc > MaksWymiar ? null : new InformacjaOObrazie(FormatObrazu.Png, (int)szerokosc, (int)wysokosc, 1);
    }

    private static byte[]? UsunMetadanePng(ReadOnlySpan<byte> dane)
    {
        using var wynik = new MemoryStream(dane.Length);
        wynik.Write(dane[..8]);
        var i = 8;
        while (i + 12 <= dane.Length)
        {
            var dlugosc = BinaryPrimitives.ReadUInt32BigEndian(dane[i..]);
            if (dlugosc > dane.Length - i - 12) return null;
            var calosc = 12 + (int)dlugosc; // długość + typ + dane + CRC
            var typ = Encoding.ASCII.GetString(dane.Slice(i + 4, 4));
            if (!MetadanePng.Contains(typ)) wynik.Write(dane.Slice(i, calosc));
            i += calosc;
            if (typ == "IEND") return wynik.ToArray();
        }
        return null; // brak IEND
    }

    // --- WebP (kontener RIFF) ----------------------------------------------------------------------------

    private static InformacjaOObrazie? RozpoznajWebP(ReadOnlySpan<byte> dane)
    {
        // Nagłówek VP8L kończy się na bajcie 25, VP8 i VP8X — na 30.
        if (dane.Length < 25) return null;
        var fragment = dane[12..16];
        int szerokosc, wysokosc;
        if (dane.Length < 30 && !fragment.SequenceEqual("VP8L"u8)) return null;
        if (fragment.SequenceEqual("VP8X"u8))
        {
            szerokosc = Le24(dane[24..]) + 1;
            wysokosc = Le24(dane[27..]) + 1;
        }
        else if (fragment.SequenceEqual("VP8L"u8))
        {
            if (dane[20] != 0x2F) return null;
            var bity = BinaryPrimitives.ReadUInt32LittleEndian(dane[21..]);
            szerokosc = (int)(bity & 0x3FFF) + 1;
            wysokosc = (int)((bity >> 14) & 0x3FFF) + 1;
        }
        else if (fragment.SequenceEqual("VP8 "u8))
        {
            if (dane[23] != 0x9D || dane[24] != 0x01 || dane[25] != 0x2A) return null;
            szerokosc = BinaryPrimitives.ReadUInt16LittleEndian(dane[26..]) & 0x3FFF;
            wysokosc = BinaryPrimitives.ReadUInt16LittleEndian(dane[28..]) & 0x3FFF;
        }
        else
        {
            return null;
        }
        return new InformacjaOObrazie(FormatObrazu.WebP, szerokosc, wysokosc, 1);

        static int Le24(ReadOnlySpan<byte> s) => s[0] | s[1] << 8 | s[2] << 16;
    }

    private static byte[]? UsunMetadaneWebP(ReadOnlySpan<byte> dane)
    {
        using var wynik = new MemoryStream(dane.Length);
        wynik.Write(dane[..12]); // RIFF, rozmiar (poprawiany na końcu), WEBP
        var i = 12;
        while (i + 8 <= dane.Length)
        {
            var dlugosc = BinaryPrimitives.ReadUInt32LittleEndian(dane[(i + 4)..]);
            var calosc = 8 + (long)dlugosc + (dlugosc & 1); // fragmenty wyrównane do parzystej długości
            if (calosc > dane.Length - i) return null;
            var typ = dane.Slice(i, 4);
            if (!typ.SequenceEqual("EXIF"u8) && !typ.SequenceEqual("XMP "u8))
            {
                var pozycja = (int)wynik.Position;
                wynik.Write(dane.Slice(i, (int)calosc));
                if (typ.SequenceEqual("VP8X"u8))
                {
                    // Flagi VP8X: bit 3 — jest EXIF, bit 2 — jest XMP. Po usunięciu fragmentów flagi muszą zgasnąć.
                    wynik.GetBuffer()[pozycja + 8] &= unchecked((byte)~0x0C);
                }
            }
            i += (int)calosc;
        }
        var tablica = wynik.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(tablica.AsSpan(4), (uint)(tablica.Length - 8));
        return tablica;
    }
}
