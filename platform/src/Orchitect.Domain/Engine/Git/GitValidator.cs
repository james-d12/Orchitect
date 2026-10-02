using System.Text.RegularExpressions;

namespace Orchitect.Domain.Engine.Git;

public static partial class GitValidator
{
    private static readonly HashSet<string> RepositorySchemes = ["https", "http", "ssh", "git"];

    public static bool IsValidRepositoryUrl(Uri? url)
    {
        if (url is null || !url.IsAbsoluteUri || !RepositorySchemes.Contains(url.Scheme))
        {
            return false;
        }

        if (string.IsNullOrEmpty(url.Host) || url.UserInfo.Contains(':'))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
        {
            return false;
        }

        return url.AbsolutePath.Trim('/').Length > 0;
    }

    public static bool IsValidCommitId(string? commitId) =>
        commitId is not null && CommitIdRegex().IsMatch(commitId);

    public static bool IsValidReference(string? reference)
    {
        if (string.IsNullOrEmpty(reference) || reference == "@")
        {
            return false;
        }

        if (reference.StartsWith('-') || reference.StartsWith('/') || reference.EndsWith('/') ||
            reference.EndsWith('.'))
        {
            return false;
        }

        if (reference.Contains("..") || reference.Contains("@{") || reference.Contains("//"))
        {
            return false;
        }

        if (reference.Any(c => char.IsControl(c) || c is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\'))
        {
            return false;
        }

        return reference.Split('/').All(component =>
            !component.StartsWith('.') && !component.EndsWith(".lock", StringComparison.Ordinal));
    }

    [GeneratedRegex("^([0-9a-fA-F]{40}|[0-9a-fA-F]{64})$")]
    private static partial Regex CommitIdRegex();
}
