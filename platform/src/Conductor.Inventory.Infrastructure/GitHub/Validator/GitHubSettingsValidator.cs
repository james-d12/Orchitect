using Conductor.Inventory.Infrastructure.GitHub.Models;
using Conductor.Inventory.Infrastructure.Shared.Observability;
using Conductor.Inventory.Infrastructure.Shared.Validation;
using Microsoft.Extensions.Configuration;

namespace Conductor.Inventory.Infrastructure.GitHub.Validator;

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