using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface ILinqMenuClient
{
    Task<Menu> GetFamilyMenuAsync(
        string buildingId,
        string districtId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<FamilyMenuIdentifierResponse> GetFamilyMenuIdentifiersAsync(
        string identifier,
        CancellationToken cancellationToken = default);
}
