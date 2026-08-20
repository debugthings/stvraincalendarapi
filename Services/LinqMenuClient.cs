using System.Net.Http.Json;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public sealed class LinqMenuClient : ILinqMenuClient
{
    private readonly IHttpClientFactory _clientFactory;

    public LinqMenuClient(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<Menu> GetFamilyMenuAsync(
        string buildingId,
        string districtId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = _clientFactory.CreateClient("LINQ");
        Menu? fetched = await client.GetFromJsonAsync<Menu>(
            $"/api/FamilyMenu?buildingId={buildingId}&districtId={districtId}&startDate={startDate:M-dd-yyyy}&endDate={endDate:M-dd-yyyy}",
            cancellationToken).ConfigureAwait(false);

        if (fetched is null)
        {
            throw new InvalidOperationException(
                $"The menu api for district {districtId}, building {buildingId}, for the time range {startDate:M-dd-yyyy} to {endDate:M-dd-yyyy} returned no content. Check the parameters and try again.");
        }

        return fetched;
    }
}
