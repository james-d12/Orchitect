using Conductor.Inventory.Infrastructure.GitLab.Models;
using Conductor.Inventory.Infrastructure.Shared.Observability;
using Conductor.Inventory.Infrastructure.Shared.Validation;
using Microsoft.Extensions.Configuration;

namespace Conductor.Inventory.Infrastructure.GitLab.Validator;

internal static class GitLabSettingsValidator
{
    internal static GitLabSettings GetValidSettings(IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        return new ValidationBuilder<GitLabSettings>(configuration)
            .SectionExists(nameof(GitLabSettings))
            .CheckEnabled(x => x.IsEnabled, nameof(GitLabSettings.IsEnabled))
            .CheckValue(x => x.HostUrl, nameof(GitLabSettings.HostUrl))
            .CheckValue(x => x.Token, nameof(GitLabSettings.Token))
            .Build();
    }
}