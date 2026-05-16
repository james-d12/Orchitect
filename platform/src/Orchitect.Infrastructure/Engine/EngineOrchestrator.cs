using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Score;
using Orchitect.Infrastructure.Engine.Score.Models;

namespace Orchitect.Infrastructure.Engine;

public interface IEngineOrchestrator
{
    Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken);
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
            ScoreFile? scoreFile = await _scoreDriver.ParseAsync(deployment, application, cancellationToken);

            if (scoreFile is null)
            {
                _logger.LogWarning("Unable to find / parse the provided score file.");
                return;
            }

            if (scoreFile.Resources is null)
            {
                _logger.LogWarning("There are no resources in the score file.");
                return;
            }

            _logger.LogInformation("Provisioning Resources for score file");

            var directoryName = scoreFile.Metadata.Name;

            var provisionInputs = new List<ProvisionInput>();

            foreach (var resource in scoreFile.Resources ?? [])
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

            await _engineProvisioner.ProvisionAsync(provisionInputs, directoryName);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while provisioning the score file.");
        }
    }
}