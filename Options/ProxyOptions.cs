namespace StVrainToICSFunctionApp.Options;

public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    public bool Enabled { get; set; }

    public string UpstreamBaseUrl { get; set; } = "https://lunchmenu.debugthings.com";
}
