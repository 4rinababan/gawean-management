using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Organizations;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).HasMaxLength(80).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(40).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();

        builder.HasMany(o => o.Members).WithOne().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.Invitations).WithOne().HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.Invitations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.UserId).HasMaxLength(450).IsRequired();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
    }
}

public sealed class OrganizationAuditLogConfiguration : IEntityTypeConfiguration<OrganizationAuditLog>
{
    public void Configure(EntityTypeBuilder<OrganizationAuditLog> builder)
    {
        builder.ToTable("organization_audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.TargetUserId).HasMaxLength(450);
        builder.Property(a => a.EventType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Detail).HasMaxLength(1000).IsRequired();
        builder.HasIndex(a => new { a.OrganizationId, a.CreatedAt });
        builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Token).HasMaxLength(64).IsRequired();
        builder.Property(i => i.InvitedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(i => i.Token).IsUnique();
        builder.HasIndex(i => new { i.OrganizationId, i.Status });
    }
}
