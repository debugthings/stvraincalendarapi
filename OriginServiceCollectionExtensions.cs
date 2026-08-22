using Microsoft.EntityFrameworkCore;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp;

public static class OriginServiceCollectionExtensions
{
    public static IServiceCollection AddLunchMenuOrigin(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.AddControllers(controllers =>
        {
            controllers.OutputFormatters.Add(new ICSTextOutputFormatter());
        });
        services.AddMemoryCache();
        services.AddLinqConnectHttpClient();
        services.AddSingleton<ILinqMenuClient, LinqMenuClient>();
        services.AddSingleton<IFastLinkSlugGenerator, FastLinkSlugGenerator>();

        string dbPath = ResolveCacheDatabasePath(configuration, contentRootPath);
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        services.AddDbContext<MenuCacheDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IMenuCacheService, MenuCacheService>();
        services.AddScoped<IFastLinkStore, FastLinkStore>();
        services.AddScoped<ISchoolDirectoryService, SchoolDirectoryService>();
        services.AddScoped<ISubscribeService, SubscribeService>();
        services.AddScoped<ILandingDayService, LandingDayService>();
        services.AddScoped<TodayMenuPageService>();
        return services;
    }

    public static void EnsureMenuCacheCreated(this IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        MenuCacheDbContext db = scope.ServiceProvider.GetRequiredService<MenuCacheDbContext>();
        db.Database.EnsureCreated();
        EnsureFastLinksTable(db);
    }

    /// <summary>
    /// EnsureCreated does not add new tables to an existing SQLite database.
    /// </summary>
    internal static void EnsureFastLinksTable(MenuCacheDbContext db)
    {
        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "FastLinks" (
                "Slug" TEXT NOT NULL CONSTRAINT "PK_FastLinks" PRIMARY KEY,
                "BuildingId" TEXT NOT NULL,
                "DistrictId" TEXT NOT NULL,
                "SchoolName" TEXT NOT NULL,
                "Session" TEXT NOT NULL,
                "DisplayTimeHhmm" INTEGER NOT NULL,
                "IncludedPlansJson" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);
    }

    internal static string ResolveCacheDatabasePath(IConfiguration configuration, string contentRootPath)
    {
        string dbPath = configuration.GetValue<string>("Cache:DatabasePath") ?? "data/menu-cache.db";
        if (Path.IsPathRooted(dbPath))
        {
            return dbPath;
        }

        // Azure App Service / Functions Linux: HOME is writable; site wwwroot may not be.
        string? siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
        string? home = Environment.GetEnvironmentVariable("HOME");
        string root = !string.IsNullOrEmpty(siteName) && !string.IsNullOrEmpty(home)
            ? home
            : contentRootPath;
        return Path.Combine(root, dbPath);
    }
}
