using Microsoft.EntityFrameworkCore;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Options;
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
        services.AddLinqConnectHttpClient();
        services.AddSingleton<ILinqMenuClient, LinqMenuClient>();

        string dbPath = ResolveCacheDatabasePath(configuration, contentRootPath);
        string? dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        services.AddDbContext<MenuCacheDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IMenuCacheService, MenuCacheService>();
        return services;
    }

    public static void EnsureMenuCacheCreated(this IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        MenuCacheDbContext db = scope.ServiceProvider.GetRequiredService<MenuCacheDbContext>();
        db.Database.EnsureCreated();
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
