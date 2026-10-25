using ECMA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECMA.Infrastructure.Data.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events", t =>
        {
            t.HasCheckConstraint("ck_events_type", "type IN (0, 1)");
            t.HasCheckConstraint("ck_events_status", "status IN (0, 1, 2)");
            t.HasCheckConstraint("ck_events_capacity", "capacity BETWEEN 1 AND 10000");
            t.HasCheckConstraint("ck_events_time", "end_at >= start_at + interval '5 minutes' AND end_at <= start_at + interval '30 days'");
            t.HasCheckConstraint("ck_events_minute_precision", "date_trunc('minute', start_at) = start_at AND date_trunc('minute', end_at) = end_at");
            t.HasCheckConstraint("ck_events_cancellation", "(status = 2) = (cancelled_at IS NOT NULL AND cancelled_by IS NOT NULL AND cancel_reason IS NOT NULL)");
            t.HasCheckConstraint("ck_events_not_published_deleted", "NOT (status = 1 AND is_deleted)");
        });
        builder.HasKey(x => x.Id).HasName("pk_events");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(100);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(3000);
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(x => x.StartAt).HasColumnName("start_at");
        builder.Property(x => x.EndAt).HasColumnName("end_at");
        builder.Property(x => x.Capacity).HasColumnName("capacity");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasMaxLength(200);
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_events_owner");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_events_cancelled_by");
        builder.HasMany(x => x.AgendaItems).WithOne().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.AgendaItems).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.OwnerId, x.CreatedAt }).HasDatabaseName("ix_events_owner_created");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_events_created");
    }
}

public sealed class AgendaItemConfiguration : IEntityTypeConfiguration<AgendaItem>
{
    public void Configure(EntityTypeBuilder<AgendaItem> builder)
    {
        builder.ToTable("agenda_items", t =>
        {
            t.HasCheckConstraint("ck_agenda_min_duration", "end_at >= start_at + interval '5 minutes'");
            t.HasCheckConstraint("ck_agenda_minute_precision", "date_trunc('minute', start_at) = start_at AND date_trunc('minute', end_at) = end_at");
        });
        builder.HasKey(x => x.Id).HasName("pk_agenda_items");
        // IDs are assigned by the domain; without this EF treats a new item reached through Event.AgendaItems as an UPDATE.
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(100);
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(1000);
        builder.Property(x => x.StartAt).HasColumnName("start_at");
        builder.Property(x => x.EndAt).HasColumnName("end_at");
        builder.HasIndex(x => new { x.EventId, x.StartAt }).HasDatabaseName("ix_agenda_event_start");
    }
}
