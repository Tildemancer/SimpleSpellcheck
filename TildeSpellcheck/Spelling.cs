using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stamp = (int Generation, bool Hyphen, int Suggestions);

namespace SimpleSpellcheck;

internal sealed class Spelling
{
    private readonly Configuration _settings;
    private readonly Action _save;
    private readonly string _lexicon;

    internal Spelling(Configuration settings, Action save, string lexicon)
    {
        _settings = settings;
        _save = save;
        _lexicon = lexicon;
    }

    private static int UnfinishedWordAt(string text)
    {
        if (text is not [.., var last] || char.IsWhiteSpace(last) || char.IsPunctuation(last) && last is not ('\'' or '-'))
            return text.Length;

        var start = text.Length;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            start--;

        return start;
    }

    private static int CommandEndsAt(string text)
    {
        if (text.Length == 0 || text[0] != '/')
            return 0;

        var end = text.IndexOf(' ');
        if (end < 0)
            return text.Length;

        // A tell's target is First Last@World or a placeholder like <t>
        if (ChannelCommands.IsTell(text))
        {
            var first = text.IndexOf(' ', end + 1);
            if (first < 0)
                return text.Length;

            if (text[end + 1] == '<')
                return first;

            var second = text.IndexOf(' ', first + 1);
            return second < 0 ? text.Length : second;
        }

        return end;
    }

    // A cut between words changes nothing since SpellCheck checks each word alone.
    // Typing at the end rechecks only the last segment for performance reasons.
    internal List<(int Index, int Length)> Marks(string text)
    {
        List<(int Index, int Length)> marks = [];

        if (!Speller.Loaded || string.IsNullOrEmpty(text))
            return marks;

        var (from, to) = (CommandEndsAt(text), UnfinishedWordAt(text));

        lock (_segments)
        {
            DropStale(_segments, ref _segmentsStamp, MostSegments);
            var cached = _segments.GetAlternateLookup<ReadOnlySpan<char>>();

            for (int start = 0, end; start < text.Length; start = end)
            {
                end = SegmentEnd(text, start);

                if (!cached.TryGetValue(text.AsSpan(start, end - start), out var found))
                {
                    var segment = text[start..end];
                    _segments[segment] = found = SpellCheck.Misspellings(segment, _settings.IgnoreWordsEndingInHyphen);
                }

                foreach (var (index, length) in found)
                    if (start + index >= from && start + index < to)
                        marks.Add((start + index, length));
            }
        }

        return marks;
    }

    internal Stamp Current => (Speller.Generation, _settings.IgnoreWordsEndingInHyphen, _settings.MaximumSuggestions);

    private void DropStale<T>(Dictionary<string, T> cache, ref Stamp stamp, int most)
    {
        if (stamp == Current && cache.Count <= most)
            return;

        cache.Clear();
        stamp = Current;
    }

    private readonly Dictionary<string, List<(int Index, int Length)>> _segments = [];
    private Stamp _segmentsStamp;

    private const int SegmentLength = 256;

    // Room for two 32,000-byte lines of ~125 segments, for a box a plugin like Emote Splitter has raised
    // DropStale empties the lot when full.
    private const int MostSegments = 256;

    private static int SegmentEnd(string text, int start)
    {
        var end = Math.Min(text.Length, start + SegmentLength);

        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        while (end < text.Length && char.IsWhiteSpace(text[end]))
            end++;

        return end;
    }

    // A hyphenated word the lexicon doesn't define is two words to Synonyms and Define, like "river-wood".
    internal (int Index, int Length)? WordAt(string text, int index)
    {
        var word = SpellCheck.WordAt(text, index);
        if (word is not var (start, length) || text.IndexOf('-', start, length) < 0 || Lexicon.Define(_lexicon, text.Substring(start, length)).Count > 0)
            return word;

        return SpellCheck.HalfAt(text, start, length, index);
    }

    private readonly Dictionary<string, Task<List<string>>> _suggesting = [];
    private Stamp _suggestingStamp;

    private const int MostSuggesting = 64;

    // The menus ask again each frame that it's null.
    internal List<string>? Suggest(string word)
    {
        if (!Speller.Loaded)
            return [];

        lock (_suggesting)
        {
            DropStale(_suggesting, ref _suggestingStamp, MostSuggesting);

            if (!_suggesting.TryGetValue(word, out var lookup))
                _suggesting[word] = lookup = Task.Run(() => Lookup(word, _settings.MaximumSuggestions));

            return lookup.IsCompleted ? lookup.Result : null;
        }
    }

    // Never throws so the menu reading its task's Result doesn't either
    private static List<string> Lookup(string word, int most)
    {
        try
        {
            return Speller.Suggest(word, most);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Spelling suggestions failed.");
            return [];
        }
    }

    internal void AddToDictionary(string word)
    {
        if (!Speller.AddWord(word))
            return;

        _settings.CustomWords.Add(word);

        // A failed save is only logged, the word is in already and the next save writes it
        try
        {
            _save();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Saving the added word failed.");
        }
    }

    private const int MostSynonyms = 10;

    // DefineWindow.Spell matches the case
    // A lowercase word's capitalized synonyms name something else (Bush's nicknames, not bush's)
    internal List<string> Synonyms(string word) =>
    [
        .. Lexicon.Synonyms(_lexicon, word)
            .SelectMany(group => group)
            .Where(synonym => !Lexicon.Closed(synonym) && !(word is [var first, ..] && char.IsLower(first) && synonym is [var cap, var next, ..] && char.IsUpper(cap) && char.IsLower(next)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MostSynonyms),
    ];
}
