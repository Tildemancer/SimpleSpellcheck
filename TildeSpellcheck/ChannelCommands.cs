using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSpellcheck;

internal static class ChannelCommands
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
    // A null separator splits at any whitespace, including the full width space.
    internal static bool IsTell(string line) => line.StartsWith('/') && TellNames.Contains(line[1..].Split(null, 2)[0]);
}
