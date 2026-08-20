using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface IMenuCacheService
{
    Task<Menu> GetMenuAsync(
        string buildingId,
        string districtId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
