using Microsoft.Extensions.Options;

namespace PrzewodnikSWPW.Web.Services;

public sealed class ZdjeciaOptions
{
    public const string Sekcja = "Zdjecia";

    /// <summary>Katalog serwowany pod adresem /media. Domyślnie wwwroot/media; na serwerze — trwały magazyn (05 §6).</summary>
    public string? KatalogMedia { get; set; }

    /// <summary>Bez skalowania po stronie serwera limit chroni użytkownika w budynku na słabym zasięgu (P-13, D-10 pkt 5).</summary>
    public int MaksRozmiarMB { get; set; } = 3;

    public long MaksRozmiarBajtow => MaksRozmiarMB * 1024L * 1024L;
}

/// <summary>Zapis plików zdjęć. Nazwę nadaje serwer (GUID + rozszerzenie z rozpoznanej zawartości), nigdy użytkownik (D-10 pkt 1).</summary>
public interface IMagazynZdjec
{
    /// <summary>Zapisuje plik i zwraca ścieżkę względną do /media, np. „zdjecia/3f2a….jpg”.</summary>
    Task<string> ZapiszAsync(byte[] dane, InformacjaOObrazie obraz, CancellationToken ct = default);

    void Usun(string sciezka);
}

public sealed class MagazynZdjec(IOptions<ZdjeciaOptions> opcje, IWebHostEnvironment srodowisko) : IMagazynZdjec
{
    public const string Podkatalog = "zdjecia";

    private string KatalogMedia => Path.GetFullPath(opcje.Value.KatalogMedia ?? Path.Combine(srodowisko.WebRootPath, "media"));

    public async Task<string> ZapiszAsync(byte[] dane, InformacjaOObrazie obraz, CancellationToken ct = default)
    {
        var sciezka = $"{Podkatalog}/{Guid.NewGuid():N}.{obraz.Rozszerzenie}";
        var pelna = PelnaSciezka(sciezka);
        Directory.CreateDirectory(Path.GetDirectoryName(pelna)!);
        // FileMode.CreateNew — nigdy nie nadpisujemy istniejącego pliku.
        await using var plik = new FileStream(pelna, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await plik.WriteAsync(dane, ct);
        return sciezka;
    }

    public void Usun(string sciezka)
    {
        var pelna = PelnaSciezka(sciezka);
        if (File.Exists(pelna)) File.Delete(pelna);
    }

    /// <summary>Ścieżka z bazy zamieniona na pełną — z blokadą wyjścia poza katalog zdjęć („../”).</summary>
    private string PelnaSciezka(string sciezka)
    {
        var katalog = Path.Combine(KatalogMedia, Podkatalog) + Path.DirectorySeparatorChar;
        var pelna = Path.GetFullPath(Path.Combine(KatalogMedia, sciezka));
        return pelna.StartsWith(katalog, StringComparison.OrdinalIgnoreCase)
            ? pelna
            : throw new InvalidOperationException($"Ścieżka zdjęcia „{sciezka}” wychodzi poza katalog zdjęć.");
    }
}
