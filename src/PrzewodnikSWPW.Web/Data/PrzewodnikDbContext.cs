using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>
/// Kontekst bazy danych. Schemat referencyjny: docs/sql/01_schemat.sql — nazwy tabel,
/// ograniczeń CHECK i indeksów unikalnych są z nim zgodne. Tabele AspNetUsers / AspNetRoles /
/// AspNetUserRoles tworzy ASP.NET Core Identity (03_ERD.md §3.4).
/// </summary>
public class PrzewodnikDbContext(DbContextOptions<PrzewodnikDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Budynek> Budynki => Set<Budynek>();
    public DbSet<Pietro> Pietra => Set<Pietro>();
    public DbSet<TypSali> TypySal => Set<TypSali>();
    public DbSet<Sala> Sale => Set<Sala>();
    public DbSet<TypPunktu> TypyPunktow => Set<TypPunktu>();
    public DbSet<PunktRuchu> PunktyRuchu => Set<PunktRuchu>();
    public DbSet<Kierunek> Kierunki => Set<Kierunek>();
    public DbSet<Utrudnienie> Utrudnienia => Set<Utrudnienie>();
    public DbSet<Zdjecie> Zdjecia => Set<Zdjecie>();
    public DbSet<ObszarAktywny> ObszaryAktywne => Set<ObszarAktywny>();
    public DbSet<Udogodnienie> Udogodnienia => Set<Udogodnienie>();
    public DbSet<PunktUdogodnienie> PunktUdogodnienia => Set<PunktUdogodnienie>();
    public DbSet<SalaUdogodnienie> SalaUdogodnienia => Set<SalaUdogodnienie>();
    public DbSet<WpisAudytu> WpisyAudytu => Set<WpisAudytu>();
    public DbSet<ZapytanieTrasy> ZapytaniaTrasy => Set<ZapytanieTrasy>();
    public DbSet<ZgloszenieDostepnosci> ZgloszeniaDostepnosci => Set<ZgloszenieDostepnosci>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        KonfigurujSlowniki(modelBuilder);
        KonfigurujStruktureBudynku(modelBuilder);
        KonfigurujGrafNawigacji(modelBuilder);
        KonfigurujMultimedia(modelBuilder);
        KonfigurujUdogodnienia(modelBuilder);
        KonfigurujEksploatacje(modelBuilder);

        // Tabele w liczbie pojedynczej, jak w docs/sql/01_schemat.sql (Budynek, a nie Budynki).
        // Wyłącznie encje domenowe — tabele Identity zachowują standardowe nazwy AspNet*.
        foreach (var encja in modelBuilder.Model.GetEntityTypes()
                     .Where(e => e.ClrType.Namespace == typeof(Budynek).Namespace))
        {
            encja.SetTableName(encja.ClrType.Name);
        }
    }

    private static void KonfigurujSlowniki(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TypSali>(e =>
        {
            e.HasIndex(t => t.Nazwa).IsUnique().HasDatabaseName("UQ_TypSali_Nazwa");
            e.HasData(
                new TypSali { Id = 1, Nazwa = "Sala komputerowa", Opis = "Pracownia ze stanowiskami komputerowymi" },
                new TypSali { Id = 2, Nazwa = "Sala ćwiczeniowa", Opis = "Sala do zajęć ćwiczeniowych" },
                new TypSali { Id = 3, Nazwa = "Sekretariat", Opis = "Sekretariat lub dziekanat" },
                new TypSali { Id = 4, Nazwa = "Sala wykładowa", Opis = "Duża sala wykładowa" },
                new TypSali { Id = 5, Nazwa = "Toaleta", Opis = "Toaleta" },
                new TypSali { Id = 6, Nazwa = "Pomieszczenie techniczne", Opis = "Niedostępne dla studentów" });
        });

        modelBuilder.Entity<TypPunktu>(e =>
        {
            e.HasIndex(t => t.Nazwa).IsUnique().HasDatabaseName("UQ_TypPunktu_Nazwa");
            e.HasData(
                new TypPunktu { Id = 1, Nazwa = "Hol", Opis = "Otwarta przestrzeń wejściowa" },
                new TypPunktu { Id = 2, Nazwa = "Korytarz", Opis = "Odcinek korytarza" },
                new TypPunktu { Id = 3, Nazwa = "Skrzyżowanie", Opis = "Miejsce, w którym korytarze się krzyżują" },
                new TypPunktu { Id = 4, Nazwa = "Podest schodów", Opis = "Spocznik przy biegu schodów" },
                new TypPunktu { Id = 5, Nazwa = "Przed windą", Opis = "Miejsce przed drzwiami windy" });
        });

        modelBuilder.Entity<Udogodnienie>(e =>
        {
            e.HasIndex(u => u.Nazwa).IsUnique().HasDatabaseName("UQ_Udogodnienie_Nazwa");
            e.HasData(
                new Udogodnienie { Id = 1, Nazwa = "Winda", Opis = "Winda osobowa z sygnalizacją głosową", Ikona = "elevator" },
                new Udogodnienie { Id = 2, Nazwa = "Pochylnia", Opis = "Podjazd dla wózków", Ikona = "ramp" },
                new Udogodnienie { Id = 3, Nazwa = "Pętla indukcyjna", Opis = "Wspomaganie aparatów słuchowych", Ikona = "hearing" },
                new Udogodnienie { Id = 4, Nazwa = "Oznaczenia brajlowskie", Opis = "Tabliczki z pismem Braille'a", Ikona = "braille" },
                new Udogodnienie { Id = 5, Nazwa = "Ścieżka dotykowa", Opis = "Prowadząca faktura w posadzce", Ikona = "path" },
                new Udogodnienie { Id = 6, Nazwa = "Oznaczenie kontrastowe", Opis = "Kontrastowe oznaczenie krawędzi", Ikona = "contrast" });
        });
    }

    private static void KonfigurujStruktureBudynku(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Budynek>(e =>
        {
            e.HasIndex(b => b.Kod).IsUnique().HasDatabaseName("UQ_Budynek_Kod");
            e.Property(b => b.CzyMaWinde).HasDefaultValue(false);
            // Sentinel = true: wartość false zawsze trafia do INSERT, a pominięta kolumna dostaje DEFAULT 1.
            e.Property(b => b.CzyAktywny).HasDefaultValue(true).HasSentinel(true);
        });

        modelBuilder.Entity<Pietro>(e =>
        {
            e.HasOne(p => p.Budynek).WithMany(b => b.Pietra)
             .HasForeignKey(p => p.BudynekId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.BudynekId, p.Numer }).IsUnique().HasDatabaseName("UQ_Pietro_Budynek_Numer");
        });

        modelBuilder.Entity<Sala>(e =>
        {
            e.HasOne(s => s.Pietro).WithMany(p => p.Sale)
             .HasForeignKey(s => s.PietroId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.TypSali).WithMany(t => t.Sale)
             .HasForeignKey(s => s.TypSaliId)
             .OnDelete(DeleteBehavior.Restrict);

            // NO ACTION, aby uniknąć wielu ścieżek kaskadowych (SQL Server, błąd 1785).
            e.HasOne(s => s.PunktWejsciowy).WithMany()
             .HasForeignKey(s => s.PunktWejsciowyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(s => new { s.PietroId, s.Symbol }).IsUnique().HasDatabaseName("UQ_Sala_Pietro_Symbol");
            e.HasIndex(s => s.Symbol).HasDatabaseName("IX_Sala_Symbol");

            e.Property(s => s.CzyDostepnaDlaWozkow).HasDefaultValue(false);
            e.Property(s => s.CzyAktywna).HasDefaultValue(true).HasSentinel(true);
        });
    }

    private static void KonfigurujGrafNawigacji(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PunktRuchu>(e =>
        {
            e.HasOne(p => p.Pietro).WithMany(p => p.PunktyRuchu)
             .HasForeignKey(p => p.PietroId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.TypPunktu).WithMany(t => t.PunktyRuchu)
             .HasForeignKey(p => p.TypPunktuId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => p.Kod).IsUnique().HasDatabaseName("UQ_PunktRuchu_Kod");

            e.Property(p => p.AzymutDomyslny).HasDefaultValue(0);
            e.Property(p => p.CzyAktywny).HasDefaultValue(true).HasSentinel(true);

            e.ToTable(t => t.HasCheckConstraint("CK_PunktRuchu_Azymut",
                "[AzymutDomyslny] IN (0, 90, 180, 270)"));
        });

        // D-04: WSZYSTKIE klucze obce encji Kierunek mają Restrict, jawnie i bez wyjątków.
        // Dwie kaskady do PunktRuchu = błąd SQL Server 1785 (P-09); kierunków nie usuwamy
        // fizycznie, tylko ustawiamy CzyAktywny = false.
        modelBuilder.Entity<Kierunek>(e =>
        {
            e.HasOne(k => k.PunktZrodlowy).WithMany(p => p.KierunkiWychodzace)
             .HasForeignKey(k => k.PunktZrodlowyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(k => k.PunktDocelowy).WithMany(p => p.KierunkiPrzychodzace)
             .HasForeignKey(k => k.PunktDocelowyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(k => k.SalaDocelowa).WithMany(s => s.KierunkiDoSali)
             .HasForeignKey(k => k.SalaDocelowaId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(k => k.KierunekPowrotny).WithMany()
             .HasForeignKey(k => k.KierunekPowrotnyId)
             .OnDelete(DeleteBehavior.Restrict);

            // Z jednego punktu tylko jeden kierunek o danym azymucie — maks. 4 wyjścia (WF-03).
            e.HasIndex(k => new { k.PunktZrodlowyId, k.Azymut }).IsUnique()
             .HasDatabaseName("UQ_Kierunek_Zrodlo_Azymut");

            e.Property(k => k.CzyAktywny).HasDefaultValue(true).HasSentinel(true);
            e.Property(k => k.CzyDostepnyBezSchodow).HasDefaultValue(true).HasSentinel(true);

            e.ToTable(t =>
            {
                // Kierunek prowadzi DOKŁADNIE do jednego celu: punktu ruchu ALBO sali.
                t.HasCheckConstraint("CK_Kierunek_JedenCel",
                    "([PunktDocelowyId] IS NOT NULL AND [SalaDocelowaId] IS NULL) OR " +
                    "([PunktDocelowyId] IS NULL AND [SalaDocelowaId] IS NOT NULL)");
                t.HasCheckConstraint("CK_Kierunek_Azymut", "[Azymut] IN (0, 90, 180, 270)");
                t.HasCheckConstraint("CK_Kierunek_Waga", "[Waga] > 0 AND [Waga] <= 200");
                t.HasCheckConstraint("CK_Kierunek_BezPetli", "[PunktZrodlowyId] <> [PunktDocelowyId]");
            });
        });

        // D-04: wszystkie klucze obce Utrudnienie — Restrict. Historia utrudnień ma zostać zachowana.
        modelBuilder.Entity<Utrudnienie>(e =>
        {
            e.HasOne(u => u.Kierunek).WithMany(k => k.Utrudnienia)
             .HasForeignKey(u => u.KierunekId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.PunktRuchu).WithMany(p => p.Utrudnienia)
             .HasForeignKey(u => u.PunktRuchuId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(u => u.DataUtworzenia).HasDefaultValueSql("SYSDATETIME()");

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Utrudnienie_Cel",
                    "([KierunekId] IS NOT NULL AND [PunktRuchuId] IS NULL) OR " +
                    "([KierunekId] IS NULL AND [PunktRuchuId] IS NOT NULL)");
                t.HasCheckConstraint("CK_Utrudnienie_Daty",
                    "[ObowiazujeDo] IS NULL OR [ObowiazujeDo] > [ObowiazujeOd]");
            });
        });
    }

    private static void KonfigurujMultimedia(ModelBuilder modelBuilder)
    {
        // D-04: wszystkie klucze obce Zdjecie — Restrict. Zdjęcie z dopracowanym tekstem
        // alternatywnym to najdroższa treść w bazie (P-02) i nie może zniknąć jako skutek uboczny.
        modelBuilder.Entity<Zdjecie>(e =>
        {
            e.HasOne(z => z.PunktRuchu).WithMany(p => p.Zdjecia)
             .HasForeignKey(z => z.PunktRuchuId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(z => z.Sala).WithMany(s => s.Zdjecia)
             .HasForeignKey(z => z.SalaId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(z => z.Kolejnosc).HasDefaultValue(0);
            e.Property(z => z.CzyDekoracyjne).HasDefaultValue(false);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Zdjecie_Wlasciciel",
                    "([PunktRuchuId] IS NOT NULL AND [SalaId] IS NULL) OR " +
                    "([PunktRuchuId] IS NULL AND [SalaId] IS NOT NULL)");
                // Dostępność wymuszona schematem: zdjęcie informacyjne musi mieć sensowny alt.
                t.HasCheckConstraint("CK_Zdjecie_Alt",
                    "[CzyDekoracyjne] = 1 OR LEN(LTRIM(RTRIM([TekstAlternatywny]))) >= 5");
            });
        });

        modelBuilder.Entity<ObszarAktywny>(e =>
        {
            e.HasOne(o => o.Zdjecie).WithMany(z => z.ObszaryAktywne)
             .HasForeignKey(o => o.ZdjecieId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(o => o.Kierunek).WithMany(k => k.ObszaryAktywne)
             .HasForeignKey(o => o.KierunekId)
             .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Obszar_Ksztalt",
                "[Ksztalt] IN ('rect', 'circle', 'poly')"));
        });
    }

    private static void KonfigurujUdogodnienia(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PunktRuchu>()
            .HasMany(p => p.Udogodnienia)
            .WithMany(u => u.PunktyRuchu)
            .UsingEntity<PunktUdogodnienie>(
                prawa => prawa.HasOne(pu => pu.Udogodnienie).WithMany(u => u.PunktUdogodnienia)
                              .HasForeignKey(pu => pu.UdogodnienieId)
                              .OnDelete(DeleteBehavior.Restrict),
                lewa => lewa.HasOne(pu => pu.PunktRuchu).WithMany(p => p.PunktUdogodnienia)
                            .HasForeignKey(pu => pu.PunktRuchuId)
                            .OnDelete(DeleteBehavior.Cascade),
                laczaca => laczaca.HasKey(pu => new { pu.PunktRuchuId, pu.UdogodnienieId }));

        modelBuilder.Entity<Sala>()
            .HasMany(s => s.Udogodnienia)
            .WithMany(u => u.Sale)
            .UsingEntity<SalaUdogodnienie>(
                prawa => prawa.HasOne(su => su.Udogodnienie).WithMany(u => u.SalaUdogodnienia)
                              .HasForeignKey(su => su.UdogodnienieId)
                              .OnDelete(DeleteBehavior.Restrict),
                lewa => lewa.HasOne(su => su.Sala).WithMany(s => s.SalaUdogodnienia)
                            .HasForeignKey(su => su.SalaId)
                            .OnDelete(DeleteBehavior.Cascade),
                laczaca => laczaca.HasKey(su => new { su.SalaId, su.UdogodnienieId }));
    }

    private static void KonfigurujEksploatacje(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WpisAudytu>(e =>
        {
            e.Property(w => w.DataOperacji).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(w => w.DataOperacji).IsDescending().HasDatabaseName("IX_WpisAudytu_Data");
        });

        modelBuilder.Entity<ZapytanieTrasy>(e =>
        {
            e.HasOne(z => z.SalaZ).WithMany()
             .HasForeignKey(z => z.SalaZId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(z => z.SalaDo).WithMany()
             .HasForeignKey(z => z.SalaDoId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(z => z.TrybWindy).HasDefaultValue(false);
            e.Property(z => z.DataZapytania).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(z => new { z.CzySukces, z.DataZapytania })
             .IsDescending(false, true)
             .HasDatabaseName("IX_ZapytanieTrasy_Sukces");
        });

        modelBuilder.Entity<ZgloszenieDostepnosci>(e =>
        {
            e.Property(z => z.Status).HasDefaultValue(StatusZgloszenia.Nowe).HasSentinel(StatusZgloszenia.Nowe);
            e.Property(z => z.DataZgloszenia).HasDefaultValueSql("SYSDATETIME()");
        });
    }
}
