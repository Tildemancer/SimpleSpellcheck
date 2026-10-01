using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace TildeSpellcheck;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; }

    public bool British { get; set; }

    public const int MostSuggestions = 10;

    public int MaximumSuggestions { get; set => field = Math.Clamp(value, 1, MostSuggestions); } = 5;

    public bool IgnoreWordsEndingInHyphen { get; set; } = true;

    public List<string> CustomWords { get; set; } = [];

    public bool LookUpOnline { get; set; } = true;
}
