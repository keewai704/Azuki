using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Azookey.Core.Update;

public sealed record ReleaseAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);

public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("prerelease")] bool Prerelease,
    [property: JsonPropertyName("assets")] IReadOnlyList<ReleaseAsset> Assets);

public sealed record ReleaseAssets(string SetupUrl, string Sha256Url);

public static class UpdateService
{
    private const string SetupAssetName = "azookey-setup.exe";
    private const string Sha256AssetName = "SHA256SUMS.txt";

    public static ReleaseAssets SelectAssets(GitHubRelease release)
    {
        ArgumentNullException.ThrowIfNull(release);

        string setupUrl = SelectAssetUrl(release, SetupAssetName);
        string sha256Url = SelectAssetUrl(release, Sha256AssetName);

        return new ReleaseAssets(setupUrl, sha256Url);
    }

    public static bool VerifySha256(byte[] bytes, string sums, string fileName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(sums);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string actual = Convert.ToHexString(SHA256.HashData(bytes));

        foreach (string line in sums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (!LineReferencesFile(parts, fileName))
            {
                continue;
            }

            if (parts.Length != 2 || !IsSha256Hex(parts[0]))
            {
                return false;
            }

            return string.Equals(parts[0], actual, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string SelectAssetUrl(GitHubRelease release, string assetName)
    {
        string url = release.Assets.Single(asset => asset.Name == assetName).BrowserDownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException($"{assetName} has an empty browser_download_url.");
        }

        return url;
    }

    private static bool LineReferencesFile(string[] parts, string fileName) =>
        parts
            .Skip(1)
            .Select(part => part.TrimStart('*'))
            .Any(part => part == fileName);

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}
