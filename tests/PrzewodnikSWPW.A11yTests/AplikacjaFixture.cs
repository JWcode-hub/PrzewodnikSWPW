using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.A11yTests;

/// <summary>
/// Aplikacja uruchomiona naprawdę (Kestrel na wolnym porcie) na własnej jednorazowej bazie z danymi
/// budynku A, plus przeglądarka Chromium. Testy dostępności potrzebują prawdziwej przeglądarki:
/// axe-core liczy kontrast z wyrenderowanych stylów, a fokus i klawisz Tab istnieją tylko w niej.
/// Serwer bazy jak w testach jednostkowych: LocalDB albo zmienna PRZEWODNIK_TESTY_SERWER (CI).
/// </summary>
public sealed class AplikacjaFixture : IAsyncLifetime
{
    public const string EmailAdministratora = "administrator@testy.test";

    private readonly string _lancuchPolaczenia;
    private readonly string _katalogMedia = Path.Combine(Path.GetTempPath(), $"przewodnik-a11y-media-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program> _fabryka = null!;
    private IPlaywright _playwright = null!;

    public AplikacjaFixture()
    {
        var serwer = Environment.GetEnvironmentVariable("PRZEWODNIK_TESTY_SERWER")
            ?? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
        _lancuchPolaczenia = $"{serwer};Database=PrzewodnikSWPW_A11y_{Guid.NewGuid():N}";
    }

    /// <summary>Adres uruchomionej aplikacji, bez końcowego ukośnika.</summary>
    public string Adres { get; private set; } = null!;

    public IBrowser Przegladarka { get; private set; } = null!;

    /// <summary>Hasło konta testowego — losowe przy każdym uruchomieniu testów.</summary>
    public string Haslo { get; } = $"Test-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}-a1!";

    public PrzewodnikDbContext UtworzKontekst() =>
        new(new DbContextOptionsBuilder<PrzewodnikDbContext>().UseSqlServer(_lancuchPolaczenia).Options);

    public async Task InitializeAsync()
    {
        await using (var db = UtworzKontekst())
        {
            await db.Database.MigrateAsync();
            await DbSeeder.ZasiejAsync(db);
        }

        // Środowisko „Testy”, nie Development: bez appsettings.Development.json i bez konta deweloperskiego.
        _fabryka = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testy")
            .UseSetting("ConnectionStrings:Default", _lancuchPolaczenia)
            .UseSetting("Zdjecia:KatalogMedia", _katalogMedia));
        _fabryka.UseKestrel(0);
        _fabryka.StartServer();
        Adres = _fabryka.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');

        using (var zakres = _fabryka.Services.CreateScope())
        {
            var uzytkownicy = zakres.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var administrator = new IdentityUser { UserName = EmailAdministratora, Email = EmailAdministratora, EmailConfirmed = true };
            var wynik = await uzytkownicy.CreateAsync(administrator, Haslo);
            Assert.True(wynik.Succeeded, string.Join(" ", wynik.Errors.Select(e => e.Description)));
            await uzytkownicy.AddToRoleAsync(administrator, Role.Administrator);
        }

        // Pobiera Chromium przy pierwszym uruchomieniu (ok. 150 MB); kolejne wywołania nic nie pobierają.
        var kod = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        Assert.True(kod == 0, $"Nie udało się zainstalować przeglądarki Chromium dla Playwright (kod {kod}).");

        _playwright = await Playwright.CreateAsync();
        Przegladarka = await _playwright.Chromium.LaunchAsync();
    }

    /// <summary>Nowa, odizolowana sesja przeglądarki (własne ciasteczka) z otwartą kartą.</summary>
    public async Task<IPage> NowaStrona(bool javaScript = true)
    {
        var kontekst = await Przegladarka.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Adres,
            Locale = "pl-PL",
            JavaScriptEnabled = javaScript,
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });
        return await kontekst.NewPageAsync();
    }

    public async Task Zaloguj(IPage strona)
    {
        await strona.GotoAsync("/konto/logowanie");
        await strona.GetByLabel("Adres e-mail").FillAsync(EmailAdministratora);
        await strona.GetByLabel("Hasło").FillAsync(Haslo);
        await strona.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();
        await strona.WaitForURLAsync("**/admin");
    }

    public async Task DisposeAsync()
    {
        await Przegladarka.DisposeAsync();
        _playwright.Dispose();
        await _fabryka.DisposeAsync();
        await using (var db = UtworzKontekst())
        {
            await db.Database.EnsureDeletedAsync();
        }

        if (Directory.Exists(_katalogMedia)) Directory.Delete(_katalogMedia, recursive: true);
    }
}

[CollectionDefinition(Nazwa)]
public sealed class AplikacjaKolekcja : ICollectionFixture<AplikacjaFixture>
{
    public const string Nazwa = "Aplikacja w przeglądarce";
}
