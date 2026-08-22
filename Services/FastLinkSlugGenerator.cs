using System.Security.Cryptography;

namespace StVrainToICSFunctionApp.Services;

public interface IFastLinkSlugGenerator
{
    string Generate();
}

public sealed class FastLinkSlugGenerator : IFastLinkSlugGenerator
{
    // Readable adjective + noun pairs for Google Calendar–friendly paths.
    private static readonly string[] Adjectives =
    [
        "fast", "bright", "calm", "clear", "crisp", "fresh", "grand", "happy", "keen", "lively",
        "merry", "noble", "open", "proud", "quick", "radiant", "solid", "sunny", "tidy", "vivid",
        "warm", "wise", "bold", "brisk", "clever", "cosy", "eager", "gentle", "golden", "honest",
    ];

    private static readonly string[] Nouns =
    [
        "lynx", "hawk", "river", "ridge", "meadow", "cedar", "pine", "falcon", "otter", "maple",
        "aspen", "brook", "canyon", "coral", "delta", "ember", "fern", "grove", "harbor", "iris",
        "jasper", "kite", "lotus", "mesa", "nest", "orchid", "pebble", "quail", "raven", "stone",
    ];

    public string Generate()
    {
        string adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        string noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        return $"{adjective}-{noun}";
    }

    public static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        int dash = slug.IndexOf('-');
        if (dash <= 0 || dash != slug.LastIndexOf('-') || dash == slug.Length - 1)
        {
            return false;
        }

        for (int i = 0; i < slug.Length; i++)
        {
            char c = slug[i];
            if (c == '-')
            {
                continue;
            }

            if (c is < 'a' or > 'z')
            {
                return false;
            }
        }

        return true;
    }
}
