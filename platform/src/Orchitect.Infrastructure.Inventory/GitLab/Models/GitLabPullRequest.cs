using System.Diagnostics.CodeAnalysis;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Infrastructure.Inventory.GitLab.Models;

[ExcludeFromCodeCoverage]
public sealed record GitLabPullRequest : PullRequest
{
}