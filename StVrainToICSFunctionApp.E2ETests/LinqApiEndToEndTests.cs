using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StVrainToICSFunctionApp.Models;
using Xunit;
using Xunit.Abstractions;

namespace StVrainToICSFunctionApp.E2ETests;

/// <summary>
/// End-to-end checks against the real LINQ API using the same HttpClient registration as the function app.
/// Requires outbound HTTPS to api.linqconnect.com.
/// </summary>
public sealed class LinqApiEndToEndTests
{
    private const string BuildingId = "67673211-c4be-ed11-82b1-880d996bcdd8";
    private const string DistrictId = "55485575-09b2-ed11-8e69-f29174b2df22";

    private readonly ITestOutputHelper _output;

    public LinqApiEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Linq_FamilyMenu_returns_OK_and_menu_JSON()
    {
        ServiceCollection services = new();
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Warning);
            b.AddConsole();
        });
        services.AddLinqConnectHttpClient();

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient("LINQ");

        DateTime start = DateTime.UtcNow.Date.AddDays(-7);
        DateTime end = DateTime.UtcNow.Date.AddDays(30);
        string path = $"/api/FamilyMenu?buildingId={BuildingId}&districtId={DistrictId}&startDate={start:M-dd-yyyy}&endDate={end:M-dd-yyyy}";

        using HttpResponseMessage response = await client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
        _output.WriteLine(body.Length > 500 ? body[..500] + "…" : body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Menu? menu = await response.Content.ReadFromJsonAsync<Menu>();
        Assert.NotNull(menu);
        bool hasPayload =
            (menu!.FamilyMenuSessions is { Length: > 0 })
            || (menu.AcademicCalendars is { Length: > 0 });
        Assert.True(hasPayload, "Expected FamilyMenuSessions or AcademicCalendars to contain data for the default date range.");
    }

    /// <summary>
    /// Optional: run <c>func start</c> (or your local host), then:
    /// <c>FUNCTIONS_E2E_BASE_URL=http://localhost:7163 dotnet test --filter Function_Lunch_menu_ics_returns_calendar</c>
    /// Port must match <c>Host.LocalHttpPort</c> in local.settings.json.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Function_Lunch_menu_ics_returns_calendar()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("FUNCTIONS_E2E_BASE_URL");
        Skip.If(string.IsNullOrWhiteSpace(baseUrl));

        using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
        string[] candidates =
        [
            $"{baseUrl.TrimEnd('/')}/api/Lunchmenu.ics",
            $"{baseUrl.TrimEnd('/')}/Lunchmenu.ics",
        ];

        HttpResponseMessage? last = null;
        foreach (string url in candidates)
        {
            last = await http.GetAsync(url);
            _output.WriteLine($"{(int)last.StatusCode} {url}");
            if (last.IsSuccessStatusCode)
                break;
            last.Dispose();
            last = null;
        }

        Assert.NotNull(last);
        using HttpResponseMessage ok = last;
        Assert.True(ok.IsSuccessStatusCode, $"Expected 2xx from one of the candidate URLs. Got {(int)ok.StatusCode} for {ok.RequestMessage?.RequestUri}");
        string ics = await ok.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VCALENDAR", ics, StringComparison.Ordinal);
        Assert.Contains("END:VCALENDAR", ics, StringComparison.Ordinal);
    }
}
