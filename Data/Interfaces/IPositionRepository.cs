using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IPositionRepository
{
    Task InsertAsync(PositionReport position, DateTime messageTime);
}