using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services;

namespace AISLiveTracking.API.Tests;

public class UnitTest1
{
    [Theory]
    [InlineData(9074729, true)]
    [InlineData(9074720, false)]
    [InlineData(123, false)]
    public void IsValidImo_ReturnsExpectedResult(int imo, bool expected)
    {
        var result = ImoValidator.IsValid(imo);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(123456789, true)]
    [InlineData(99999999, false)]
    [InlineData(1000000000, false)]
    public void IsValidMmsi_ReturnsExpectedResult(long mmsi, bool expected)
    {
        var result = PositionValidator.IsValidMmsi(mmsi);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(91, 0, true)]
    [InlineData(0, 181, true)]
    [InlineData(0, 0, false)]
    public void IsUnavailableAisFix_ReturnsExpectedResult(
        double latitude,
        double longitude,
        bool expected)
    {
        var result = PositionValidator.IsUnavailableAisFix(
            latitude,
            longitude);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-90, true)]
    [InlineData(90, true)]
    [InlineData(-90.1, false)]
    [InlineData(90.1, false)]
    public void IsValidLatitude_ReturnsExpectedResult(
        double latitude,
        bool expected)
    {
        var result = PositionValidator.IsValidLatitude(latitude);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-180, true)]
    [InlineData(180, true)]
    [InlineData(-180.1, false)]
    [InlineData(180.1, false)]
    public void IsValidLongitude_ReturnsExpectedResult(
        double longitude,
        bool expected)
    {
        var result = PositionValidator.IsValidLongitude(longitude);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123456789", null, true)]
    [InlineData("9074729", null, true)]
    [InlineData("12345678", null, false)]
    [InlineData("12A456789", null, false)]
    [InlineData("123456789", "mmsi", true)]
    [InlineData("9074729", "imo", true)]
    [InlineData("123456789", "imo", false)]
    [InlineData("9074729", "mmsi", false)]
    [InlineData("9074729", "unknown", false)]
    public void IsValidIdentifier_ReturnsExpectedResult(
        string identifier,
        string? idType,
        bool expected)
    {
        var resolver = new IdentifierResolver(
            new InMemoryVesselRepository());

        var result = resolver.IsValidIdentifier(identifier, idType);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ResolveMmsiAsync_WithImo_ReturnsVesselMmsi()
    {
        var repository = new InMemoryVesselRepository(
            new Vessel
            {
                Mmsi = 123456789,
                Imo = 9074729
            });

        var resolver = new IdentifierResolver(repository);

        var result = await resolver.ResolveMmsiAsync("9074729");

        Assert.Equal(123456789, result);
    }

    [Fact]
    public async Task ResolveMmsiAsync_WithMmsi_ReturnsMmsi()
    {
        var repository = new InMemoryVesselRepository(
            new Vessel
            {
                Mmsi = 123456789,
                Imo = 9074729
            });

        var resolver = new IdentifierResolver(repository);

        var result = await resolver.ResolveMmsiAsync("123456789");

        Assert.Equal(123456789, result);
    }

    [Fact]
    public async Task ResolveMmsiAsync_WhenVesselDoesNotExist_ReturnsNull()
    {
        var resolver = new IdentifierResolver(
            new InMemoryVesselRepository());

        var result = await resolver.ResolveMmsiAsync("123456789");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0, "Under way using engine")]
    [InlineData(1, "At anchor")]
    [InlineData(5, "Moored")]
    [InlineData(15, "Not defined")]
    [InlineData(99, "Unknown")]
    public void NavStatusMapper_ReturnsExpectedText(
        int navStatus,
        string expected)
    {
        var result = NavStatusMapper.GetText(navStatus);

        Assert.Equal(expected, result);
    }

    private sealed class InMemoryVesselRepository : IVesselRepository
    {
        private readonly List<Vessel> _vessels;

        public InMemoryVesselRepository(params Vessel[] vessels)
        {
            _vessels = vessels.ToList();
        }

        public Task<Vessel?> GetByMmsiAsync(long mmsi)
        {
            var vessel = _vessels.FirstOrDefault(
                item => item.Mmsi == mmsi);

            return Task.FromResult(vessel);
        }

        public Task<Vessel?> GetByImoAsync(int imo)
        {
            var vessel = _vessels.FirstOrDefault(
                item => item.Imo == imo);

            return Task.FromResult(vessel);
        }

        public Task UpsertAsync(Vessel vessel)
        {
            var existing = _vessels.FirstOrDefault(
                item => item.Mmsi == vessel.Mmsi);

            if (existing != null)
            {
                _vessels.Remove(existing);
            }

            _vessels.Add(vessel);

            return Task.CompletedTask;
        }
    }
}