using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
{
    // Autoryzacja na poziomie całego obszaru Admin (niezależnie od atrybutów na akcjach).
    o.Conventions.Add(new AutoryzacjaObszaruAdmin());

    // Komunikaty wiązania modelu po polsku — domyślne są angielskie („The value 'x' is not valid”).
    var k = o.ModelBindingMessageProvider;
    k.SetValueIsInvalidAccessor(w => $"Wartość „{w}” jest nieprawidłowa.");
    k.SetAttemptedValueIsInvalidAccessor((w, pole) => $"Wartość „{w}” w polu „{pole}” jest nieprawidłowa.");
    k.SetValueMustBeANumberAccessor(pole => $"Pole „{pole}” musi być liczbą. Część dziesiętną oddziel przecinkiem, np. 5,5.");
    k.SetValueMustNotBeNullAccessor(pole => $"Pole „{pole}” jest wymagane.");
    k.SetMissingBindRequiredValueAccessor(pole => $"Brak wartości pola „{pole}”.");
    k.SetMissingKeyOrValueAccessor(() => "Brak wymaganej wartości.");
    k.SetMissingRequestBodyRequiredValueAccessor(() => "Brak danych formularza.");
    k.SetNonPropertyAttemptedValueIsInvalidAccessor(w => $"Wartość „{w}” jest nieprawidłowa.");
    k.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Podana wartość jest nieprawidłowa.");
    k.SetNonPropertyValueMustBeANumberAccessor(() => "Wartość musi być liczbą.");
    k.SetUnknownValueIsInvalidAccessor(pole => $"Wartość w polu „{pole}” jest nieprawidłowa.");
});

// Polskie znaki w HTML jako znaki, nie encje (&#x17A;). Znaki specjalne HTML (<, >, &, ") nadal są kodowane.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Łańcuch połączenia nigdy w repozytorium: lokalnie appsettings.Development.json (w .gitignore)
// albo user-secrets, na serwerze zmienna środowiskowa ConnectionStrings__Default.
var lancuchPolaczenia = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Brak łańcucha połączenia „ConnectionStrings:Default”. Instrukcja konfiguracji: README.md.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBiezacyUzytkownik, BiezacyUzytkownik>();
builder.Services.AddScoped<AudytInterceptor>();
builder.Services.AddScoped<UniewaznianieGrafuInterceptor>();

builder.Services.AddDbContext<PrzewodnikDbContext>((uslugi, options) => options
    .UseSqlServer(lancuchPolaczenia)
    .AddInterceptors(uslugi.GetRequiredService<AudytInterceptor>(), uslugi.GetRequiredService<UniewaznianieGrafuInterceptor>()));

// Tożsamość: hasła min. 12 znaków, blokada po 5 nieudanych próbach na 15 minut (05 §8, P-19).
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 12;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<PrzewodnikDbContext>()
    .AddErrorDescriber<PolskieBledyTozsamosci>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/konto/logowanie";
    o.LogoutPath = "/konto/wylogowanie";
    o.AccessDeniedPath = "/konto/brak-dostepu";
    o.Cookie.Name = "przewodnik_sesja";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
});

builder.Services.AddScoped<INawigacjaRepozytorium, NawigacjaRepozytorium>();
builder.Services.AddScoped<NawigacjaService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGrafBudynkuCache, GrafBudynkuCache>();
builder.Services.AddSingleton<GeneratorOpisuService>();
builder.Services.AddScoped<ITrasaRepozytorium, TrasaRepozytorium>();
builder.Services.AddScoped<WyszukiwarkaTrasService>();
builder.Services.Configure<KontaktAlternatywnyOptions>(builder.Configuration.GetSection(KontaktAlternatywnyOptions.Sekcja));
builder.Services.AddScoped<IWyszukiwarkaRepozytorium, WyszukiwarkaRepozytorium>();
builder.Services.AddScoped<WyszukiwarkaSalService>();
builder.Services.AddScoped<UstawieniaDostepnosciService>();
builder.Services.AddScoped<IAdministracjaRepozytorium, AdministracjaRepozytorium>();
builder.Services.AddScoped<AdministracjaService>();
builder.Services.AddScoped<WalidatorGrafuService>();

var app = builder.Build();

using (var zakres = app.Services.CreateScope())
{
    await TozsamoscSeeder.ZasiejRoleAsync(zakres.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());

    if (app.Environment.IsDevelopment())
    {
        // Dane przykładowe budynku A i konto administratora — wyłącznie w środowisku deweloperskim.
        await DbSeeder.ZasiejAsync(zakres.ServiceProvider.GetRequiredService<PrzewodnikDbContext>());
        await TozsamoscSeeder.ZasiejAdministratoraDevAsync(
            zakres.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(), app.Configuration, app.Logger);
    }
}

if (!app.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(app.Configuration[$"{KontaktAlternatywnyOptions.Sekcja}:Telefon"]))
{
    // Art. 7 ustawy o dostępności cyfrowej: przy braku trasy musimy podać alternatywny sposób dostępu.
    // Numer portierni dostarcza dział IT Uczelni (sprawa O-02) — do tego czasu komunikat pokazuje tylko opis.
    app.Logger.LogWarning("Brak numeru telefonu portierni ({Sekcja}:Telefon) — komunikat o braku trasy nie poda numeru.",
        KontaktAlternatywnyOptions.Sekcja);
}

if (!app.Environment.IsDevelopment())
{
    // Strona błędu bez szczegółów technicznych — szczegóły wyłącznie do logu.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Jedna kultura — pl-PL — niezależnie od serwera i nagłówka Accept-Language: liczby dziesiętne
// z przecinkiem („5,5”) wiążą się tak samo lokalnie i na serwerze w chmurze.
var polski = new CultureInfo("pl-PL");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(polski),
    SupportedCultures = [polski],
    SupportedUICultures = [polski],
    RequestCultureProviders = [],
});

// Zdjęcia w wwwroot/media są wgrywane w czasie działania aplikacji, więc nie ma ich
// w manifeście MapStaticAssets — obsługuje je UseStaticFiles.
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

/// <summary>Widoczny dla testów integracyjnych (WebApplicationFactory).</summary>
public partial class Program;
