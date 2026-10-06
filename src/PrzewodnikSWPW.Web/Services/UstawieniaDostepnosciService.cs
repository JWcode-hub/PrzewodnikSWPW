using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Odczyt i zapis ustawień dostępności w ciasteczku. Ciasteczko jest niezbędne do świadczenia
/// usługi, o którą prosi użytkownik (zapamiętanie jego wyboru), i nie służy do śledzenia.
/// </summary>
public sealed class UstawieniaDostepnosciService(IHttpContextAccessor dostepDoKontekstu)
{
    public const string NazwaCiasteczka = "przewodnik_ustawienia";
    private const string KluczWKontekscie = nameof(UstawieniaDostepnosci);

    private static readonly JsonSerializerOptions OpcjeJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Ustawienia bieżącego żądania; przy braku lub uszkodzeniu ciasteczka — domyślne.</summary>
    public UstawieniaDostepnosci Pobierz()
    {
        var kontekst = dostepDoKontekstu.HttpContext;
        if (kontekst is null)
        {
            return UstawieniaDostepnosci.Domyslne;
        }

        if (kontekst.Items.TryGetValue(KluczWKontekscie, out var zapamietane) && zapamietane is UstawieniaDostepnosci u)
        {
            return u;
        }

        var ustawienia = Odczytaj(kontekst.Request.Cookies[NazwaCiasteczka]);
        kontekst.Items[KluczWKontekscie] = ustawienia;
        return ustawienia;
    }

    public void Zapisz(UstawieniaDostepnosci ustawienia)
    {
        var kontekst = dostepDoKontekstu.HttpContext
            ?? throw new InvalidOperationException("Zapis ustawień wymaga kontekstu żądania HTTP.");

        kontekst.Response.Cookies.Append(NazwaCiasteczka, JsonSerializer.Serialize(ustawienia, OpcjeJson), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            Secure = kontekst.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
        kontekst.Items[KluczWKontekscie] = ustawienia;
    }

    private static UstawieniaDostepnosci Odczytaj(string? wartosc)
    {
        if (string.IsNullOrEmpty(wartosc))
        {
            return UstawieniaDostepnosci.Domyslne;
        }

        try
        {
            var ustawienia = JsonSerializer.Deserialize<UstawieniaDostepnosci>(wartosc, OpcjeJson);
            return ustawienia is { CzyPoprawne: true } ? ustawienia : UstawieniaDostepnosci.Domyslne;
        }
        catch (JsonException)
        {
            return UstawieniaDostepnosci.Domyslne;
        }
    }
}
