using System.Net.Http;

namespace StVrainToICSFunctionApp;

// Same as linq main bundle: append("Linq-Nutrition-Url", uuidv4()) per request.
internal sealed class LinqNutritionUrlHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? pinned = Environment.GetEnvironmentVariable("LinqNutritionUrl", EnvironmentVariableTarget.Process);
        string value = string.IsNullOrWhiteSpace(pinned)
            ? Guid.NewGuid().ToString("D").ToLowerInvariant()
            : pinned.Trim();

        request.Headers.TryAddWithoutValidation("linq-nutrition-url", value);
        return base.SendAsync(request, cancellationToken);
    }
}
