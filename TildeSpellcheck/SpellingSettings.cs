using System;
using System.Collections.Generic;

namespace TildeTools.Modules.Spelling;

[Serializable]
public sealed class SpellingSettings
{
    public bool British { get; set; }

    public const int MostSuggestions = 10;

    public int MaximumSuggestions { get; set => field = Math.Clamp(value, 1, MostSuggestions); } = 5;

    public bool IgnoreWordsEndingInHyphen { get; set; } = true;

    public List<string> CustomWords { get; set; } = [];

    public bool LookUpOnline { get; set; } = true;
}
