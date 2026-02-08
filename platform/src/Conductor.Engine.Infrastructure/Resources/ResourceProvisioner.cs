using Conductor.Engine.Domain.Application;
using Conductor.Engine.Domain.Deployment;
using Conductor.Engine.Domain.ResourceTemplate;
using Conductor.Engine.Infrastructure.Score;
using Conductor.Engine.Infrastructure.Score.Models;
using Microsoft.Extensions.Logging;

namespace Conductor.Engine.Infrastructure.Resources;

public interface IResourceProvisioner
{
    Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken);
}

public sealed class ResourceProvisioner : IResourceProvisioner
{
    private readonly ILogger<ResourceProvisioner> _logger;
    private readonly IResourceTemplateRepository _resourceTemplateRepository;
    private readonly IResourceFactory _resourceFactory;
    private readonly IScoreDriver _scoreDriver;

    public ResourceProvisioner(ILogger<ResourceProvisioner> logger, IScoreDriver scoreDriver,
        IResourceTemplateRepository resourceTemplateRepository, IResourceFactory resourceFactory)
    {
        _logger = logger;
        _scoreDriver = scoreDriver;
        _resourceTemplateRepository = resourceTemplateRepository;
        _resourceFactory = resourceFactory;
    }

    public async Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken)
    {
        ScoreFile? scoreFile = await _scoreDriver.ParseAsync(deployment, application, cancellationToken);

        if (scoreFile is null)
        {
            _logger.LogWarning("Unable to find / parse the provided score file.");
            return;
        }

        if (scoreFile.Resources is not null)
        {
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

            await _resourceFactory.ProvisionAsync(provisionInputs, directoryName);
        }
    }
}