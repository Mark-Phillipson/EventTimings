using System.Linq;
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using EventTimings.Api;
using EventTimings.Api.Data;

namespace EventTimings.Api.Tests;

public sealed class TimingStoreTests
{
    private sealed class SimpleDbContextFactory : IDbContextFactory<EventTimingsDbContext>, IDisposable
    {
        private readonly DbContextOptions<EventTimingsDbContext> options;
        private readonly string dbPath;

        public SimpleDbContextFactory(DbContextOptions<EventTimingsDbContext> options, string dbPath)
        {
            this.options = options;
            this.dbPath = dbPath;
        }

        public EventTimingsDbContext CreateDbContext() => new EventTimingsDbContext(options);

        public void Dispose()
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
            catch { }
        }
    }

    private static string CreateTempDbPath() => Path.Combine(Path.GetTempPath(), $"eventtimings_test_{Guid.NewGuid():N}.db");

    [Test]
    public void RemoveRiderFromWave_RemovesRider_WhenNotStarted()
    {
        var dbPath = CreateTempDbPath();

        var options = new DbContextOptionsBuilder<EventTimingsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using var factory = new SimpleDbContextFactory(options, dbPath);
        var store = new TimingStore(factory);

        // Create a wave
        var wave = store.CreateWave("UnitTestWave");
        Assert.IsNotNull(wave);

        // Ensure seeded riders exist
        var riders = store.GetRiders();
        Assert.IsNotNull(riders);
        Assert.IsNotEmpty(riders);

        var riderId = riders[0].RiderId;

        // Assign the rider
        var (assigned, assignError) = store.AssignRiderToWave(wave.WaveId, riderId);
        Assert.IsNull(assignError, "Assign should not return an error");
        Assert.IsTrue(assigned.RiderIds.Contains(riderId), "Rider should be assigned to the wave");

        // Now remove the rider
        var (removed, removeError) = store.RemoveRiderFromWave(wave.WaveId, riderId);
        Assert.IsNull(removeError, "Remove should not return an error");
        Assert.IsFalse(removed.RiderIds.Contains(riderId), "Rider should be removed from the wave");
    }
}
