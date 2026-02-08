using Microsoft.Extensions.Configuration;
using Orchitect.Inventory.Infrastructure.GitHub.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Infrastructure.Shared.Validation;

namespace Orchitect.Inventory.Infrastructure.GitHub.Validator;

internal static class GitHubSettingsValidator
{
    internal static GitHubSettings GetValidSettings(IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        return new ValidationBuilder<GitHubSettings>(configuration)
            .SectionExists(nameof(GitHubSettings))
            .CheckEnabled(x => x.IsEnabled, nameof(GitHubSettings.IsEnabled))
            .CheckValue(x => x.AgentName, nameof(GitHubSettings.AgentName))
            .CheckValue(x => x.Token, nameof(GitHubSettings.Token))
            .Build();
    }
}