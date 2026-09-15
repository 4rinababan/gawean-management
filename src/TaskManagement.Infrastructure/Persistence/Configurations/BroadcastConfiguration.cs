using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Broadcasts;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public sealed class BroadcastConfiguration : IEntityTypeConfiguration<Broadcast>
{
    public void Configure(EntityTypeBuilder<Broadcast> builder)
    {
        builder.ToTable("broadcasts");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Message).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.ImageStorageKey).HasMaxLength(500);
        builder.Property(b => b.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(b => b.TargetType).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(b => new { b.IsActive, b.CreatedAt });
    }
}

public sealed class BroadcastTargetUserConfiguration : IEntityTypeConfiguration<BroadcastTargetUser>
{
    public void Configure(EntityTypeBuilder<BroadcastTargetUser> builder)
    {
        builder.ToTable("broadcast_target_users");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(t => new { t.BroadcastId, t.UserId }).IsUnique();
        builder.HasOne<Broadcast>().WithMany().HasForeignKey(t => t.BroadcastId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BroadcastDismissalConfiguration : IEntityTypeConfiguration<BroadcastDismissal>
{
    public void Configure(EntityTypeBuilder<BroadcastDismissal> builder)
    {
        builder.ToTable("broadcast_dismissals");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(d => new { d.BroadcastId, d.UserId }).IsUnique();
        builder.HasIndex(d => d.UserId);
        builder.HasOne<Broadcast>().WithMany().HasForeignKey(d => d.BroadcastId).OnDelete(DeleteBehavior.Cascade);
    }
}
