using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>Plik z formularza — bez zależności od ASP.NET, żeby reguły dało się testować bez żądania HTTP.</summary>
public sealed record WgrywanyPlik(string NazwaOryginalna, string TypMime, byte[] Dane);

/// <summary>
/// Zdjęcia i aktywne obszary w panelu administratora (WF-26, WF-27, WN-39, D-09, D-10).
/// Plik jest sprawdzany trzy razy niezależnie: rozszerzenie z białej listy, typ MIME z białej listy
/// i rozpoznana zawartość — wszystkie trzy muszą wskazywać ten sam format.
/// </summary>
public sealed class ZdjeciaService(IAdministracjaRepozytorium repo, IMagazynZdjec magazyn, IOptions<ZdjeciaOptions> opcje)
{
    public static readonly IReadOnlyDictionary<string, FormatObrazu> DozwoloneRozszerzenia = new Dictionary<string, FormatObrazu>
    {
        [".jpg"] = FormatObrazu.Jpeg,
        [".jpeg"] = FormatObrazu.Jpeg,
        [".png"] = FormatObrazu.Png,
        [".webp"] = FormatObrazu.WebP,
    };

    public static readonly IReadOnlyDictionary<string, FormatObrazu> DozwoloneTypyMime = new Dictionary<string, FormatObrazu>
    {
        ["image/jpeg"] = FormatObrazu.Jpeg,
        ["image/png"] = FormatObrazu.Png,
        ["image/webp"] = FormatObrazu.WebP,
    };

    /// <summary>Wartość atrybutu accept pola pliku — tylko podpowiedź dla przeglądarki; decyduje serwer.</summary>
    public const string AtrybutAccept = ".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp";

    public long MaksRozmiarBajtow => opcje.Value.MaksRozmiarBajtow;
    public int MaksRozmiarMB => opcje.Value.MaksRozmiarMB;

    // --- Plik ---------------------------------------------------------------------------------------------

    /// <summary>Reguły pliku: obecność, rozmiar, rozszerzenie, MIME, zawartość. Zwraca obraz albo błąd pola „Plik”.</summary>
    public static (InformacjaOObrazie? Obraz, string? Blad) SprawdzPlik(WgrywanyPlik? plik, long maksBajtow)
    {
        if (plik is null || plik.Dane.Length == 0)
            return (null, "Wybierz plik zdjęcia.");
        if (plik.Dane.Length > maksBajtow)
            return (null, KomunikatZaDuzyPlik(plik.Dane.Length, maksBajtow));

        var rozszerzenie = Path.GetExtension(plik.NazwaOryginalna).ToLowerInvariant();
        if (!DozwoloneRozszerzenia.TryGetValue(rozszerzenie, out var zRozszerzenia))
            return (null, $"Pliki {(rozszerzenie.Length > 0 ? rozszerzenie : "bez rozszerzenia")} nie są przyjmowane. Wgraj zdjęcie JPG, PNG albo WebP.");
        if (!DozwoloneTypyMime.TryGetValue(plik.TypMime.ToLowerInvariant(), out var zMime) || zMime != zRozszerzenia)
            return (null, "Typ pliku nie zgadza się z jego rozszerzeniem. Wgraj zdjęcie JPG, PNG albo WebP.");

        var obraz = Obrazy.Rozpoznaj(plik.Dane);
        if (obraz is null)
            return (null, "Plik nie jest poprawnym zdjęciem JPG, PNG ani WebP — mógł zostać uszkodzony albo ma zmienione rozszerzenie.");
        if (obraz.Format != zRozszerzenia)
            return (null, $"Rozszerzenie {rozszerzenie} nie zgadza się z zawartością pliku (to obraz {obraz.Format.ToString().ToUpperInvariant()}). " +
                          "Zapisz zdjęcie ponownie w programie graficznym.");
        return (obraz, null);
    }

    private static readonly CultureInfo Polski = new("pl-PL");

    public static string KomunikatZaDuzyPlik(long rozmiar, long maksBajtow) =>
        string.Create(Polski, $"Plik ma {rozmiar / 1024.0 / 1024.0:0.0} MB, a limit to {maksBajtow / 1024 / 1024} MB. ") +
        "Zmniejsz zdjęcie do szerokości około 1600 pikseli i wgraj je ponownie.";

    // --- Zdjęcia ------------------------------------------------------------------------------------------

    public Task<List<Zdjecie>> ZdjeciaWlasciciela(int? punktId, int? salaId, CancellationToken ct = default) =>
        repo.Zapytanie<Zdjecie>().AsNoTracking().Include(z => z.ObszaryAktywne)
            .Where(z => punktId != null ? z.PunktRuchuId == punktId : z.SalaId == salaId)
            .OrderBy(z => z.Kolejnosc).ThenBy(z => z.Id).ToListAsync(ct);

    public Task<Zdjecie?> PobierzZdjecie(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Zdjecie>().Include(z => z.PunktRuchu).Include(z => z.Sala)
            .Include(z => z.ObszaryAktywne).ThenInclude(o => o.Kierunek)
            .FirstOrDefaultAsync(z => z.Id == id, ct);

    /// <summary>Nazwa właściciela do nagłówków i linków, np. „punkt A-0-P04” albo „sala A15”; null — brak właściciela.</summary>
    public async Task<string?> NazwaWlasciciela(int? punktId, int? salaId, CancellationToken ct = default)
    {
        if (punktId is int p)
            return await repo.Zapytanie<PunktRuchu>().Where(x => x.Id == p).Select(x => "punkt " + x.Kod).FirstOrDefaultAsync(ct);
        if (salaId is int s)
            return await repo.Zapytanie<Sala>().Where(x => x.Id == s).Select(x => "sala " + x.Symbol).FirstOrDefaultAsync(ct);
        return null;
    }

    /// <summary>
    /// Nowe zdjęcie: sprawdza plik i właściciela, usuwa metadane (D-10 pkt 2), zapisuje plik pod nazwą nadaną
    /// przez serwer i dopiero potem rekord. Gdy zapis do bazy się nie uda, plik jest usuwany.
    /// </summary>
    public async Task<WynikOperacji> DodajZdjecie(Zdjecie z, WgrywanyPlik? plik, CancellationToken ct = default)
    {
        var (obraz, blad) = SprawdzPlik(plik, MaksRozmiarBajtow);
        if (blad is not null) return WynikOperacji.Blad("Plik", blad);
        if (await SprawdzWlasciciela(z, ct) is { } bladWlasciciela) return bladWlasciciela;

        var czyste = Obrazy.UsunMetadane(plik!.Dane, obraz!);
        if (czyste is null)
            return WynikOperacji.Blad("Plik", "Plik jest uszkodzony — nie udało się odczytać jego struktury. Zapisz zdjęcie ponownie w programie graficznym.");

        z.SciezkaPliku = await magazyn.ZapiszAsync(czyste, obraz!, ct);
        z.Szerokosc = obraz!.Szerokosc;
        z.Wysokosc = obraz.Wysokosc;
        try
        {
            repo.Dodaj(z);
            await repo.ZapiszAsync(ct);
        }
        catch
        {
            magazyn.Usun(z.SciezkaPliku);
            throw;
        }
        return WynikOperacji.Ok();
    }

    /// <summary>Zmiana opisu, źródła, licencji, kolejności — bez wymiany pliku.</summary>
    public async Task<WynikOperacji> ZapiszZdjecie(Zdjecie z, CancellationToken ct = default)
    {
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    private async Task<WynikOperacji?> SprawdzWlasciciela(Zdjecie z, CancellationToken ct)
    {
        if (z.PunktRuchuId.HasValue == z.SalaId.HasValue)
            return WynikOperacji.Blad("", "Zdjęcie musi należeć do punktu ruchu albo do sali.");
        if (z.PunktRuchuId is int p && !await repo.Zapytanie<PunktRuchu>().AnyAsync(x => x.Id == p, ct))
            return WynikOperacji.Blad("", "Nie znaleziono punktu ruchu, do którego ma należeć zdjęcie.");
        if (z.SalaId is int s && !await repo.Zapytanie<Sala>().AnyAsync(x => x.Id == s, ct))
            return WynikOperacji.Blad("", "Nie znaleziono sali, do której ma należeć zdjęcie.");
        return null;
    }

    // --- Aktywne obszary (WF-27) --------------------------------------------------------------------------

    /// <summary>Kierunki, do których może prowadzić obszar — wychodzące z punktu, do którego należy zdjęcie.</summary>
    public Task<List<Kierunek>> KierunkiDlaObszarow(int punktId, CancellationToken ct = default) =>
        repo.Zapytanie<Kierunek>().AsNoTracking().Include(k => k.PunktDocelowy).Include(k => k.SalaDocelowa)
            .Where(k => k.PunktZrodlowyId == punktId).OrderBy(k => k.Azymut).ToListAsync(ct);

    public Task<ObszarAktywny?> PobierzObszar(int id, CancellationToken ct = default) =>
        repo.Zapytanie<ObszarAktywny>().Include(o => o.Zdjecie).ThenInclude(z => z.PunktRuchu).Include(o => o.Kierunek)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <summary>Dodaje albo zapisuje obszar. Kierunek musi wychodzić z punktu zdjęcia, współrzędne — mieścić się w zdjęciu.</summary>
    public async Task<WynikOperacji> ZapiszObszar(ObszarAktywny o, CancellationToken ct = default)
    {
        var zdjecie = await repo.Zapytanie<Zdjecie>().AsNoTracking().FirstOrDefaultAsync(z => z.Id == o.ZdjecieId, ct);
        if (zdjecie is null) return WynikOperacji.Blad("", "Nie znaleziono zdjęcia.");
        if (zdjecie.PunktRuchuId is not int punkt)
            return WynikOperacji.Blad("", "Aktywne obszary można dodać tylko do zdjęcia punktu ruchu — prowadzą w jeden z jego kierunków.");

        var bledy = new List<BladPola>();
        if (!await repo.Zapytanie<Kierunek>().AnyAsync(k => k.Id == o.KierunekId && k.PunktZrodlowyId == punkt, ct))
            bledy.Add(new BladPola(nameof(ObszarAktywny.KierunekId), "Wybierz z listy kierunek wychodzący z punktu, do którego należy zdjęcie."));
        var (wspolrzedne, blad) = WspolrzedneObszaru.Sprawdz(o.Ksztalt, o.Wspolrzedne, zdjecie.Szerokosc, zdjecie.Wysokosc);
        if (blad is not null) bledy.Add(new BladPola(nameof(ObszarAktywny.Wspolrzedne), blad));
        if (bledy.Count > 0) return new WynikOperacji(bledy);

        o.Wspolrzedne = wspolrzedne!;
        o.Etykieta = o.Etykieta.Trim();
        if (o.Id == 0) repo.Dodaj(o);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    /// <summary>
    /// Obszar usuwamy fizycznie: to fragment zdjęcia, nie rekord grafu — D-04 i usuwanie logiczne dotyczą
    /// kierunków, zdjęć i utrudnień. Zmiana i tak trafia do rejestru zmian (audyt).
    /// </summary>
    public async Task<WynikOperacji> UsunObszar(int id, CancellationToken ct = default)
    {
        var o = await repo.ZnajdzAsync<ObszarAktywny>(id, ct);
        if (o is null) return WynikOperacji.Blad("", "Nie znaleziono obszaru.");
        repo.Usun(o);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }
}
