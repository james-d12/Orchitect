using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Storage.Azure;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Storage;

public static class StorageExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(StorageOptions.SectionName);

        var options = section.Get<StorageOptions>() ?? new StorageOptions();

        if (options.GetValidationError() is { } error)
        {
            throw new InvalidOperationException(error);
        }

        services.AddOptions<StorageOptions>().Bind(section);

        switch (options.Type)
        {
            case StorageProviderType.None:
                break;
            case StorageProviderType.FileSystem:
                services.TryAddSingleton<IStorageProvider>(new FileSystemStorageProvider(options.FileSystem));
                break;
            case StorageProviderType.AzureBlob:
                var blobOptions = options.AzureBlob;
                services.TryAddSingleton<IStorageProvider>(_ => new AzureBlobStorageProvider(
                    new BlobContainerClient(blobOptions.ContainerUri!, AzureStorageCredential.Create(blobOptions))));
                break;
            default:
                throw new InvalidOperationException(
                    $"{StorageOptions.SectionName}:Type '{options.Type}' is not supported.");
        }

        return services;
    }
}
