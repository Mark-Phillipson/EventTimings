using Microsoft.EntityFrameworkCore;

namespace EventTimings.Api.Data;

public sealed class EventTimingsDbContext(DbContextOptions<EventTimingsDbContext> options) : DbContext(options)
{
    public DbSet<RiderEntity> Riders => Set<RiderEntity>();

    public DbSet<RouteTypeEntity> RouteTypes => Set<RouteTypeEntity>();

    public DbSet<OfficialEntity> Officials => Set<OfficialEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RiderEntity>(entity =>
        {
            entity.HasKey(item => item.RiderId);
            entity.Property(item => item.RiderId).HasMaxLength(64);
            entity.Property(item => item.BibNumber).HasMaxLength(32).IsRequired();
            entity.Property(item => item.FullName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(128).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
            entity.HasIndex(item => item.BibNumber).IsUnique();

            entity.HasOne(item => item.RouteType)
                .WithMany(route => route.Riders)
                .HasForeignKey(item => item.RouteTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RouteTypeEntity>(entity =>
        {
            entity.HasKey(item => item.RouteTypeId);
            entity.Property(item => item.RouteTypeId).HasMaxLength(64);
            entity.Property(item => item.Name).HasMaxLength(128).IsRequired();
            entity.Property(item => item.DistanceMiles).IsRequired();
            entity.Property(item => item.IsActive).IsRequired();
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<OfficialEntity>(entity =>
        {
            entity.HasKey(item => item.OfficialId);
            entity.Property(item => item.OfficialId).HasMaxLength(64);
            entity.Property(item => item.FullName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.PinHash).HasMaxLength(256).IsRequired();
            entity.Property(item => item.PinSalt).HasMaxLength(128).IsRequired();
            entity.Property(item => item.IsActive).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
            entity.HasIndex(item => item.FullName).IsUnique();
        });
    }
}

public sealed class RiderEntity
{
    public string RiderId { get; set; } = string.Empty;

    public string BibNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? RouteTypeId { get; set; }

    public RouteTypeEntity? RouteType { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RouteTypeEntity
{
    public string RouteTypeId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int DistanceMiles { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RiderEntity> Riders { get; set; } = [];
}

public sealed class OfficialEntity
{
    public string OfficialId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PinHash { get; set; } = string.Empty;

    public string PinSalt { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
