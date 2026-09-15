using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Wiki;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public sealed class WikiPageConfiguration : IEntityTypeConfiguration<WikiPage>
{
    public void Configure(EntityTypeBuilder<WikiPage> builder)
    {
        builder.ToTable("wiki_pages");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Title).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Content).HasMaxLength(50000);
        builder.Property(w => w.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(w => w.LastEditedByUserId).HasMaxLength(450);
        builder.HasIndex(w => new { w.OrganizationId, w.ParentPageId });

        // Deleting a parent page cascades to its children (a subtree removed together); the FK's own
        // "no self-cascade-path" restriction is why this can't also cascade from Organization directly
        // through Issue/Comment-style chains — it's fine here since WikiPage has no other cascade parent.
        builder.HasOne<WikiPage>().WithMany().HasForeignKey(w => w.ParentPageId).OnDelete(DeleteBehavior.Cascade);
    }
}
