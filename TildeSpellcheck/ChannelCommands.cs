using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SimpleSpellcheck;

internal static partial class ChannelCommands
{
    private static HashSet<string> TellNames = new(["tell", "t"], StringComparer.OrdinalIgnoreCase);

    // TextCommand sheet rows, where DE or FR clients keep stuff like /sagen and /dire.
    // Filled into a copy so each row only matches the built-in names and not one any earlier rows added.
    internal static void AddClientNames(IEnumerable<IEnumerable<string>> rows)
    {
        var tells = new HashSet<string>(TellNames, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var names = row.Select(n => n.TrimStart('/')).Where(n => n.Length > 0).ToList();
            if (names.Any(TellNames.Contains))
                tells.UnionWith(names);
        }

        TellNames = tells;
    }

    // A /t or /tell, whether or not its target reads as a name.
    internal static bool IsTell(string line) =>
        CommandRegex().Match(line) is { Success: true } match && TellNames.Contains(match.Groups["cmd"].Value);

    [GeneratedRegex(@"^/(?<cmd>\p{L}+[0-9]*)(?:\s+(?<rest>.*))?$", RegexOptions.Singleline)]
    private static partial Regex CommandRegex();
}
