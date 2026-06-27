using Azookey.Core.Update;
using Xunit;

namespace Azookey.Core.Tests.Update;

public sealed class UpdateServiceTests
{
    [Fact]
    public void SelectsSetupAndSha256Assets()
    {
        var release = new GitHubRelease("v1", false, [
            new ReleaseAsset("azookey-setup.exe", "https://example.test/setup.exe"),
            new ReleaseAsset("SHA256SUMS.txt", "https://example.test/SHA256SUMS.txt")
        ]);

        ReleaseAssets assets = UpdateService.SelectAssets(release);

        Assert.Equal("https://example.test/setup.exe", assets.SetupUrl);
        Assert.Equal("https://example.test/SHA256SUMS.txt", assets.Sha256Url);
    }

    [Fact]
    public void RejectsEmptyAssetUrl()
    {
        var release = new GitHubRelease("v1", false, [
            new ReleaseAsset("azookey-setup.exe", ""),
            new ReleaseAsset("SHA256SUMS.txt", "https://example.test/SHA256SUMS.txt")
        ]);

        Assert.Throws<InvalidOperationException>(() => UpdateService.SelectAssets(release));
    }

    [Fact]
    public void RejectsSha256Mismatch()
    {
        byte[] bytes = "abc"u8.ToArray();
        string sums = "0000000000000000000000000000000000000000000000000000000000000000  azookey-setup.exe";

        Assert.False(UpdateService.VerifySha256(bytes, sums, "azookey-setup.exe"));
    }

    [Theory]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  azookey-setup.exe")]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD *azookey-setup.exe")]
    public void AcceptsMatchingSha256FromSumsText(string matchingLine)
    {
        byte[] bytes = "abc"u8.ToArray();
        string sums = $"1111111111111111111111111111111111111111111111111111111111111111  other.exe\r\n{matchingLine}\n";

        Assert.True(UpdateService.VerifySha256(bytes, sums, "azookey-setup.exe"));
    }

    [Theory]
    [InlineData("not-a-hash  azookey-setup.exe")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  other.exe")]
    public void RejectsMissingOrMalformedSha256Lines(string sums)
    {
        Assert.False(UpdateService.VerifySha256("abc"u8.ToArray(), sums, "azookey-setup.exe"));
    }

    [Theory]
    [InlineData("not-a-hash  azookey-setup.exe\r\nba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  azookey-setup.exe")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  ignored-token  azookey-setup.exe")]
    public void RejectsMalformedTargetSha256LinesWithoutSkippingToFallbackMatch(string sums)
    {
        Assert.False(UpdateService.VerifySha256("abc"u8.ToArray(), sums, "azookey-setup.exe"));
    }
}
