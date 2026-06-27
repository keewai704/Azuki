using System.Xml.Linq;
using Xunit;

namespace Azookey.UI.Tests.Candidate;

public sealed class ImeWindowThemeTests
{
    [Theory]
    [InlineData("CandidateWindow.xaml", "CandidateText1")]
    [InlineData("CandidateWindow.xaml", "CandidateText2")]
    [InlineData("CandidateWindow.xaml", "CandidateText3")]
    [InlineData("CandidateWindow.xaml", "CandidateText4")]
    [InlineData("CandidateWindow.xaml", "CandidateText5")]
    [InlineData("IndicatorWindow.xaml", "ModeText")]
    public void TextOnWhiteToolWindowsUsesExplicitForeground(string xamlFileName, string textBlockName)
    {
        XDocument document = XDocument.Load(GetWindowXamlPath(xamlFileName));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement textBlock = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "TextBlock" &&
                string.Equals(element.Attribute(xaml + "Name")?.Value, textBlockName, StringComparison.Ordinal));

        Assert.Equal("#FF202020", textBlock.Attribute("Foreground")?.Value);
    }

    private static string GetWindowXamlPath(string xamlFileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "apps", "Azookey.UI", "Windows", xamlFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {xamlFileName} from {AppContext.BaseDirectory}.");
    }
}
