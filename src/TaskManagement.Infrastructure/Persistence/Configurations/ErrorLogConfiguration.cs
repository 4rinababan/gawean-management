using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Diagnostics;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public sealed class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("error_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Level).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Exception).HasMaxLength(8000);
        builder.HasIndex(e => e.CreatedAt);
    }
}
