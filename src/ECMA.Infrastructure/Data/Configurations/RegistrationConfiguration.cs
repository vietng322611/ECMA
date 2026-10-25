using ECMA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECMA.Infrastructure.Data.Configurations;

public sealed class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ToTable("registrations", t =>
        {
            t.HasCheckConstraint("ck_registrations_status", "status IN (0, 1)");
            t.HasCheckConstraint("ck_registrations_code", "code ~ '^REG-[A-Z0-9]{12}$'");
            t.HasCheckConstraint("ck_registrations_cancellation",
                "(status = 1) = (cancel_source IS NOT NULL AND cancelled_at IS NOT NULL AND cancelled_by IS NOT NULL AND cancel_reason IS NOT NULL) " +
                "AND (status = 0) = (cancel_source IS NULL AND cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL)");
            t.HasCheckConstraint("ck_registrations_cancel_source", "cancel_source IS NULL OR cancel_source IN (0, 1, 2)");
            t.HasCheckConstraint("ck_registrations_check_in", "(checked_in_at IS NULL) = (checked_in_by IS NULL) AND (checked_in_at IS NULL OR status = 0)");
        });
        builder.HasKey(x => x.Id).HasName("pk_registrations");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(16);
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.RegisteredAt).HasColumnName("registered_at");
        builder.Property(x => x.CancelSource).HasColumnName("cancel_source");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasMaxLength(200);
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CheckedInAt).HasColumnName("checked_in_at");
        builder.Property(x => x.CheckedInBy).HasColumnName("checked_in_by");
        builder.Ignore(x => x.IsCheckedIn);
        builder.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_registrations_event");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_registrations_user");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_registrations_cancelled_by");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CheckedInBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_registrations_checked_in_by");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_registrations_code");
        builder.HasIndex(x => new { x.EventId, x.UserId }).IsUnique().HasDatabaseName("uq_registrations_event_user");
        builder.HasIndex(x => new { x.EventId, x.RegisteredAt }).HasDatabaseName("ix_registrations_event_registered");
        builder.HasIndex(x => new { x.UserId, x.RegisteredAt }).HasDatabaseName("ix_registrations_user_registered");
    }
}
