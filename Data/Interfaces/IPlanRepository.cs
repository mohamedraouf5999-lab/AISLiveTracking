using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IPlanRepository
{
    Task<Plan?> GetByIdAsync(int id);
}