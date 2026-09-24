namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

/// <summary>
/// The local module directory for a downloaded source, or the reason it could not be downloaded.
/// </summary>
public sealed record TerraformModuleDownloadResult(string? Directory, string? Error)
{
    public bool IsSuccess => Directory is not null;

    public static TerraformModuleDownloadResult Success(string directory) => new(directory, null);

    public static TerraformModuleDownloadResult Failure(string error) => new(null, error);
}
