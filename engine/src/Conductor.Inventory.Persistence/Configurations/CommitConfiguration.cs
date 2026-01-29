using Conductor.Inventory.Domain.Git;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Inventory.Persistence.Configurations;

internal sealed class CommitConfiguration : IEntityTypeConfiguration<Commit>
{
    public void Configure(EntityTypeBuilder<Commit> builder)
    {
        builder.ToTable("Commits");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => new CommitId(value)
            );

        builder.Property(c => c.Url).IsRequired();
        builder.Property(c => c.Committer).IsRequired();
        builder.Property(c => c.Comment);
        builder.Property(c => c.ChangeCount);
    }
}
