using System.Collections.Immutable;
using Conductor.Inventory.Domain.Git;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Inventory.Persistence.Configurations;

internal sealed class PullRequestConfiguration : IEntityTypeConfiguration<PullRequest>
{
    public void Configure(EntityTypeBuilder<PullRequest> builder)
    {
        builder.ToTable("PullRequests");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.Id)
            .HasConversion(
                id => id.Value,
                value => new PullRequestId(value)
            );

        builder.Property(pr => pr.Name).IsRequired();
        builder.Property(pr => pr.Description).IsRequired();
        builder.Property(pr => pr.Url).IsRequired();
        builder.Property(pr => pr.Status).IsRequired().HasConversion<string>();
        builder.Property(pr => pr.Platform).IsRequired().HasConversion<string>();
        builder.Property(pr => pr.RepositoryUrl).IsRequired();
        builder.Property(pr => pr.RepositoryName).IsRequired();
        builder.Property(pr => pr.CreatedOnDate).IsRequired();

        builder.Property(pr => pr.Labels)
            .HasConversion(
                labels => string.Join(',', labels),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToImmutableHashSet()
            );

        builder.Property(pr => pr.Reviewers)
            .HasConversion(
                reviewers => string.Join(',', reviewers),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToImmutableHashSet()
            );

        builder.OwnsOne(pr => pr.LastCommit, c =>
        {
            c.Property(x => x.Id)
                .HasConversion(
                    id => id.Value,
                    value => new CommitId(value)
                );
            c.Property(x => x.Url).IsRequired();
            c.Property(x => x.Committer).IsRequired();
            c.Property(x => x.Comment);
            c.Property(x => x.ChangeCount);
        });
    }
}
