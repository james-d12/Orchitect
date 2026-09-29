using System.Diagnostics.CodeAnalysis;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

[ExcludeFromCodeCoverage]
public sealed record AzureDevOpsPullRequest : PullRequest;