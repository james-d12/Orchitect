using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Configuration.Score;
using Orchitect.Infrastructure.Engine.Configuration.Score.Models;
using Orchitect.Infrastructure.Engine.Provisioner;

namespace Orchitect.Infrastructure.Engine;

public interface IEngineOrchestrator
{
    Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken);

    /// <summary>
    /// Destroys a deployment's resources using the same project and state as <see cref="StartAsync"/>.
    /// </summary>
    Task DestroyAsync(Application application, Deployment deployment, CancellationToken cancellationToken);
}

public sealed class EngineOrchestrator : IEngineOrchestrator
{
    private readonly ILogger<EngineOrchestrator> _logger;
    private readonly IResourceTemplateRepository _resourceTemplateRepository;
    private readonly IEngineProvisioner _engineProvisioner;
    private readonly IScoreDriver _scoreDriver;

    public EngineOrchestrator(ILogger<EngineOrchestrator> logger, IScoreDriver scoreDriver,
        IResourceTemplateRepository resourceTemplateRepository, IEngineProvisioner engineProvisioner)
    {
        _logger = logger;
        _scoreDriver = scoreDriver;
        _resourceTemplateRepository = resourceTemplateRepository;
        _engineProvisioner = engineProvisioner;
    }

    public async Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        try
        {
            var provisionRequest = await BuildProvisionInputsAsync(application, deployment, cancellationToken);

            if (provisionRequest is null)
            {
                return;
            }

            _logger.LogInformation("Provisioning Resources for score file");

            await _engineProvisioner.ProvisionAsync(provisionRequest.Value.Inputs, provisionRequest.Value.Context,
                cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while provisioning the score file.");
            throw;
        }
    }

    public async Task DestroyAsync(Application application, Deployment deployment,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        try
        {
            var provisionRequest = await BuildProvisionInputsAsync(application, deployment, cancellationToken);

            if (provisionRequest is null)
            {
                return;
            }

            _logger.LogInformation("Destroying Resources for score file");

            await _engineProvisioner.DeleteAsync(provisionRequest.Value.Inputs, provisionRequest.Value.Context,
                cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while destroying the score file.");
            throw;
        }
    }

    private async Task<(ProvisionContext Context, List<ProvisionInput> Inputs)?> BuildProvisionInputsAsync(
        Application application, Deployment deployment, CancellationToken cancellationToken)
    {
        ScoreFile? scoreFile = await _scoreDriver.ParseAsync(deployment, application, cancellationToken);

        if (scoreFile is null)
        {
            _logger.LogWarning("Unable to find / parse the provided score file.");
            return null;
        }

        if (scoreFile.Resources is null)
        {
            _logger.LogWarning("There are no resources in the score file.");
            return null;
        }

        var context = new ProvisionContext(
            ProjectName: scoreFile.Metadata.Name,
            ApplicationId: deployment.ApplicationId.Value.ToString(),
            EnvironmentId: deployment.EnvironmentId.Value.ToString());

        var provisionInputs = new List<ProvisionInput>();

        foreach (var resource in scoreFile.Resources)
        {
            var type = resource.Value.Type.Trim().ToLower();
            var inputs = resource.Value.Parameters;

            ResourceTemplate? resourceTemplate =
                await _resourceTemplateRepository.GetByTypeAsync(type, cancellationToken);

            if (resourceTemplate is null)
            {
                _logger.LogInformation("Could not get resource template for: {Type}", type);
                continue;
            }

            if (inputs is null)
            {
                _logger.LogInformation("No inputs present in the score file");
                continue;
            }

            provisionInputs.Add(new ProvisionInput(resourceTemplate, inputs, resource.Key));
        }

        return (context, provisionInputs);
    }
}
