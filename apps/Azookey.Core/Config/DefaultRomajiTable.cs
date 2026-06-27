namespace Azookey.Core.Config;

public static class DefaultRomajiTable
{
    public static List<RomajiRule> Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "default_romaji_table.txt");
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "crates",
                "shared",
                "src",
                "default_romaji_table.txt"));
        }

        return ParseRows(File.ReadLines(path));
    }

    public static List<RomajiRule> ParseRows(IEnumerable<string> lines)
    {
        var rows = new List<RomajiRule>();

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            string[] parts = trimmed.Split('\t');
            if (parts.Length < 2)
            {
                continue;
            }

            string input = parts[0].Trim();
            string output = parts[1].Trim();
            if (input.Length == 0 || output.Length == 0)
            {
                continue;
            }

            rows.Add(new RomajiRule
            {
                Input = input,
                Output = output,
                NextInput = parts.Length > 2 ? parts[2].Trim() : ""
            });
        }

        return rows;
    }
}

public static class CharacterWidthDefaults
{
    private static readonly (string Symbol, bool IsFullwidth)[] Symbols =
    [
        ("0", false), ("1", false), ("2", false), ("3", false), ("4", false), ("5", false),
        ("6", false), ("7", false), ("8", false), ("9", false), ("!", true), ("\"", true),
        ("#", false), ("$", false), ("%", false), ("&", false), ("'", true), ("(", true),
        (")", true), ("*", true), ("+", true), (",", true), ("-", true), (".", true),
        ("/", true), (":", true), (";", true), ("<", true), ("=", true), (">", true),
        ("?", true), ("@", false), ("[", true), ("\\", false), ("]", true), ("^", false),
        ("_", false), ("`", false), ("{", true), ("|", false), ("}", true), ("~", true)
    ];

    public static Dictionary<string, bool> CreateSymbolMap()
    {
        return Symbols.ToDictionary(pair => pair.Symbol, pair => pair.IsFullwidth, StringComparer.Ordinal);
    }
}
