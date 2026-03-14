using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class PullRequestConfiguration : IEntityTypeConfiguration<PullRequest>
{
    public void Configure(EntityTypeBuilder<PullRequest> builder)
    {
        builder.ToTable("PullRequests", "inventory");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.Id)
            .HasConversion(
                id => id.Value,
                value => new PullRequestId(value)
            );

        // Organisation FK with cascade delete
        builder.Property(pr => pr.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(pr => pr.OrganisationId)
            .HasConstraintName("FK_PullRequests_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(pr => pr.Name).IsRequired().HasMaxLength(500);
        builder.Property(pr => pr.Description).IsRequired();
        builder.Property(pr => pr.Url).IsRequired().HasMaxLength(2000);
        builder.Property(pr => pr.Status).IsRequired().HasConversion<string>();
        builder.Property(pr => pr.Platform).IsRequired().HasConversion<string>();
        builder.Property(pr => pr.RepositoryUrl).IsRequired().HasMaxLength(2000);
        builder.Property(pr => pr.RepositoryName).IsRequired().HasMaxLength(500);
        builder.Property(pr => pr.CreatedOnDate).IsRequired();

        // Audit timestamps
        builder.Property(pr => pr.DiscoveredAt).IsRequired();
        builder.Property(pr => pr.UpdatedAt).IsRequired();

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

        // Indexes
        builder.HasIndex(pr => pr.OrganisationId)
            .HasDatabaseName("IX_PullRequests_OrganisationId");

        builder.HasIndex(pr => pr.RepositoryUrl)
            .HasDatabaseName("IX_PullRequests_RepositoryUrl");

        builder.HasIndex(pr => pr.Status)
            .HasDatabaseName("IX_PullRequests_Status")
            .HasFilter("\"Status\" IN ('Active', 'Draft')");
    }
}
