using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PrzewodnikSWPW.Web.Data;

namespace PrzewodnikSWPW.UnitTests.Data;

/// <summary>
/// Tworzy jednorazową bazę SQL Server (domyślnie LocalDB) z migracjami i danymi z <see cref="DbSeeder"/>,
/// a po testach ją usuwa. Prawdziwy SQL Server, a nie InMemory — dzięki temu seeder jest
/// sprawdzany także przez ograniczenia CHECK i klucze obce.
/// W CI serwer można wskazać zmienną środowiskową PRZEWODNIK_TESTY_SERWER.
/// </summary>
public sealed class BazaTestowaFixture : IAsyncLifetime
{
    private readonly string _lancuchPolaczenia;

    public string LancuchPolaczenia => _lancuchPolaczenia;

    public BazaTestowaFixture()
    {
        var serwer = Environment.GetEnvironmentVariable("PRZEWODNIK_TESTY_SERWER")
            ?? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
        _lancuchPolaczenia = $"{serwer};Database=PrzewodnikSWPW_Testy_{Guid.NewGuid():N}";
    }

    public PrzewodnikDbContext UtworzKontekst(params IInterceptor[] interceptory) =>
        new(new DbContextOptionsBuilder<PrzewodnikDbContext>()
            .UseSqlServer(_lancuchPolaczenia)
            .AddInterceptors(interceptory)
            .Options);

    public async Task InitializeAsync()
    {
        await using var db = UtworzKontekst();
        await db.Database.MigrateAsync();
        await DbSeeder.ZasiejAsync(db);
    }

    public async Task DisposeAsync()
    {
        await using var db = UtworzKontekst();
        await db.Database.EnsureDeletedAsync();
    }
}

/// <summary>Jedna zasiana baza współdzielona przez wszystkie klasy testów, które jej potrzebują.</summary>
[CollectionDefinition(Nazwa)]
public sealed class BazaTestowaKolekcja : ICollectionFixture<BazaTestowaFixture>
{
    public const string Nazwa = "Baza testowa";
}
