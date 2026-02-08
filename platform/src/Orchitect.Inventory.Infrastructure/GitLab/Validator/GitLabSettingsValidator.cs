using Microsoft.Extensions.Configuration;
using Orchitect.Inventory.Infrastructure.GitLab.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Infrastructure.Shared.Validation;

namespace Orchitect.Inventory.Infrastructure.GitLab.Validator;

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