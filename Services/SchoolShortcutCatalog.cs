using StVrainToICSFunctionApp.Options;

namespace StVrainToICSFunctionApp.Services;

public sealed class SchoolShortcutCatalog
{
    private readonly Dictionary<string, SchoolShortcut> _schools;

    public SchoolShortcutCatalog(IConfiguration configuration)
    {
        Dictionary<string, SchoolShortcut> bound =
            configuration.GetSection("SchoolShortcuts").Get<Dictionary<string, SchoolShortcut>>()
            ?? new Dictionary<string, SchoolShortcut>();
        _schools = new Dictionary<string, SchoolShortcut>(bound, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string? school, out SchoolShortcut shortcut)
    {
        shortcut = null!;
        return !string.IsNullOrWhiteSpace(school) && _schools.TryGetValue(school, out shortcut!);
    }

    public IReadOnlyList<KeyValuePair<string, SchoolShortcut>> All() =>
        _schools
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
