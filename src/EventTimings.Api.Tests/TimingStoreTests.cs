using System.Linq;
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using EventTimings.Api;
using EventTimings.Api.Data;
using EventTimings.Contracts;

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

    [Test]
    public void TimingSessions_PersistAcrossStoreRestart()
    {
        var dbPath = CreateTempDbPath();

        var options = new DbContextOptionsBuilder<EventTimingsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using var factory = new SimpleDbContextFactory(options, dbPath);
        var store = new TimingStore(factory);

        var rider = store.GetRiders().First();
        var startRequest = new TimingCommandRequest("marden-2026", "Mark Phillipson", "2468", rider.RiderId);

        var startResult = store.StartTiming(startRequest);
        Assert.IsTrue(startResult.Success, "Start timing should succeed.");

        var stopResult = store.StopTiming(startRequest);
        Assert.IsTrue(stopResult.Success, "Stop timing should succeed.");

        // Simulate API restart by creating a new TimingStore over the same database.
        var restartedStore = new TimingStore(factory);
        var sessionsPage = restartedStore.GetTimingSessionsPaged(0, 20);

        Assert.That(sessionsPage.TotalCount, Is.EqualTo(1), "Timing sessions should persist in DB across store restart.");
        Assert.That(sessionsPage.Items[0].StoppedAt, Is.Not.Null, "Stopped time should persist in DB.");
    }

    [Test]
    public void ResetAllTimings_AllowsOnlyMarkPhillipson()
    {
        var dbPath = CreateTempDbPath();

        var options = new DbContextOptionsBuilder<EventTimingsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using var factory = new SimpleDbContextFactory(options, dbPath);
        var store = new TimingStore(factory);

        var rider = store.GetRiders().First();
        var startRequest = new TimingCommandRequest("marden-2026", "Mark Phillipson", "2468", rider.RiderId);

        var startResult = store.StartTiming(startRequest);
        Assert.IsTrue(startResult.Success, "Start timing should succeed before reset checks.");

        var unauthorizedReset = store.ResetAllTimings(new TimingCommandRequest("marden-2026", "Lorna Stafford", "0707", rider.RiderId));
        Assert.IsFalse(unauthorizedReset.Success, "Reset should be denied for non-Mark officials.");

        var afterUnauthorized = store.GetTimingSessionsPaged(0, 20);
        Assert.That(afterUnauthorized.TotalCount, Is.EqualTo(1), "Denied reset must not clear sessions.");

        var authorizedReset = store.ResetAllTimings(new TimingCommandRequest("marden-2026", "Mark Phillipson", "2468", rider.RiderId));
        Assert.IsTrue(authorizedReset.Success, "Reset should be allowed for Mark Phillipson.");

        var afterAuthorized = store.GetTimingSessionsPaged(0, 20);
        Assert.That(afterAuthorized.TotalCount, Is.EqualTo(0), "Authorized reset should clear persisted sessions.");
    }
}
