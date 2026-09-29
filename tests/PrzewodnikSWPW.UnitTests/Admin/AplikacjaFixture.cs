using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PrzewodnikSWPW.UnitTests.Data;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>
/// Cała aplikacja w pamięci (WebApplicationFactory) na WŁASNEJ jednorazowej bazie. Środowisko „Testy”,
/// a nie Development — dzięki temu nie wczytuje się appsettings.Development.json z łańcuchem połączenia
/// do bazy deweloperskiej i kontem administratora. Konta testowe powstają tu, z losowymi hasłami.
/// </summary>
public sealed partial class AplikacjaFixture : IAsyncLifetime
{
    public const string EmailAdministratora = "administrator@testy.test";
    public const string EmailRedaktora = "redaktor@testy.test";
    public const string EmailDoBlokady = "blokada@testy.test";

    public BazaTestowaFixture Baza { get; } = new();
    public WebApplicationFactory<Program> Fabryka { get; private set; } = null!;

    /// <summary>Katalog /media na czas testów — wgrywane zdjęcia nie trafiają do wwwroot w repozytorium.</summary>
    public string KatalogMedia { get; } = Path.Combine(Path.GetTempPath(), $"przewodnik-testy-media-{Guid.NewGuid():N}");

    /// <summary>Hasło wspólne dla kont testowych — losowe przy każdym uruchomieniu testów.</summary>
    public string Haslo { get; } = $"Test-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}-a1!";

    public async Task InitializeAsync()
    {
        await Baza.InitializeAsync();
        Fabryka = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testy")
            .UseSetting("ConnectionStrings:Default", Baza.LancuchPolaczenia)
            .UseSetting("Zdjecia:KatalogMedia", KatalogMedia));

        using var zakres = Fabryka.Services.CreateScope();
        var uzytkownicy = zakres.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await Utworz(uzytkownicy, EmailAdministratora, Role.Administrator);
        await Utworz(uzytkownicy, EmailRedaktora, Role.Redaktor);
        await Utworz(uzytkownicy, EmailDoBlokady, Role.Administrator);
    }

    private async Task Utworz(UserManager<IdentityUser> uzytkownicy, string email, string rola)
    {
        var u = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var wynik = await uzytkownicy.CreateAsync(u, Haslo);
        Assert.True(wynik.Succeeded, string.Join(" ", wynik.Errors.Select(e => e.Description)));
        await uzytkownicy.AddToRoleAsync(u, rola);
    }

    public HttpClient Klient() => Fabryka.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static string Token(string html) =>
        TokenRegex().Match(html) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : throw new InvalidOperationException("Brak tokenu antiforgery na stronie.");

    /// <summary>Loguje klienta i zwraca odpowiedź na POST logowania.</summary>
    public async Task<HttpResponseMessage> Zaloguj(HttpClient klient, string email, string haslo)
    {
        var strona = await klient.GetStringAsync("/konto/logowanie");
        return await klient.PostAsync("/konto/logowanie", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(strona),
            ["Email"] = email,
            ["Haslo"] = haslo,
        }));
    }

    public async Task DisposeAsync()
    {
        await Fabryka.DisposeAsync();
        await Baza.DisposeAsync();
        if (Directory.Exists(KatalogMedia)) Directory.Delete(KatalogMedia, recursive: true);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}

[CollectionDefinition(Nazwa)]
public sealed class AplikacjaKolekcja : ICollectionFixture<AplikacjaFixture>
{
    public const string Nazwa = "Aplikacja";
}
