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
}
