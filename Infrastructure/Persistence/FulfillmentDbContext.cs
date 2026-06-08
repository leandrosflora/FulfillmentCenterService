using FulfillmentCenterService.Domain;
using FulfillmentCenterService.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Infrastructure.Persistence;

public sealed class FulfillmentDbContext : DbContext
{
    public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options) { }

    public DbSet<FulfillmentCenter> FulfillmentCenters => Set<FulfillmentCenter>();
    public DbSet<CenterCoverage> CenterCoverages => Set<CenterCoverage>();
    public DbSet<SellerCenterEnrollment> SellerCenterEnrollments => Set<SellerCenterEnrollment>();
    public DbSet<CapacitySlot> CapacitySlots => Set<CapacitySlot>();
    public DbSet<CapacityReservation> CapacityReservations => Set<CapacityReservation>();
    public DbSet<CenterOperationSchedule> OperationSchedules => Set<CenterOperationSchedule>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FulfillmentCenter>(entity =>
        {
            entity.ToTable("fulfillment_centers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Region).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.MaximumWeightKg).HasPrecision(10, 3);
            entity.Property(x => x.MaximumCubicWeightKg).HasPrecision(10, 3);
        });

        modelBuilder.Entity<CapacitySlot>(entity =>
        {
            entity.ToTable("capacity_slots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.FulfillmentCenterId, x.OperationDate, x.Mode }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("ck_capacity_slots_allocated_capacity", "\"TotalCapacityUnits\" >= 0 AND \"ReservedCapacityUnits\" >= 0 AND \"ConsumedCapacityUnits\" >= 0 AND \"ReservedCapacityUnits\" + \"ConsumedCapacityUnits\" <= \"TotalCapacityUnits\""));
        });

        modelBuilder.Entity<CapacityReservation>(entity =>
        {
            entity.ToTable("capacity_reservations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.Status, x.ExpiresAt });
        });

        modelBuilder.Entity<CenterCoverage>(entity =>
        {
            entity.ToTable("center_coverages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.PostalCodeFrom, x.PostalCodeTo, x.Mode });
        });

        modelBuilder.Entity<SellerCenterEnrollment>(entity =>
        {
            entity.ToTable("seller_center_enrollments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.SellerId, x.FulfillmentCenterId, x.Mode }).IsUnique();
        });

        modelBuilder.Entity<CenterOperationSchedule>(entity =>
        {
            entity.ToTable("center_operation_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.FulfillmentCenterId, x.OperationDate, x.Mode }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
        });
    }
}
