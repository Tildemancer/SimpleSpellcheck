using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace TildeSpellcheck;

// The spelling menu's entries, drawn into the editbox menu.
// Kept by menu id and word, so a menu's synonyms and corrections are asked for once.
internal sealed class SpellMenu(Spelling spelling)
{
    private sealed class Shown(int id, string word)
    {
        internal readonly int Id = id;
        internal readonly string Word = word;
        internal List<string>? Synonyms, Corrections;
    }

    private Shown? _shown;

    // True once it's done with the word and its been defined, added, ignored or replaced.
    internal bool Draw(int id, string word, bool misspelled, Action<string> replace)
    {
        if (_shown is not { } shown || shown.Id != id || shown.Word != word)
            _shown = shown = new(id, word);

        ImGui.TextDisabled(word);

        // ##entry keeps an entry's id apart from a correction's, so you can correct 'defin' to 'Define' without tainting.
        if (ImGui.Selectable("Synonyms##entry", false, ImGuiSelectableFlags.DontClosePopups))
            shown.Synonyms = shown.Synonyms is null ? spelling.Synonyms(word) : null;

        if (shown.Synonyms is not null)
        {
            // A synonym can share a label with a correction.
            using var synonyms = ImRaii.PushId("synonyms");
            using var indent = ImRaii.PushIndent();

            if (shown.Synonyms.Count == 0)
                ImGui.TextDisabled("None found");

            foreach (var synonym in shown.Synonyms)
                if (ImGui.Selectable(synonym))
                {
                    spelling.Define(synonym, word, replace);
                    return true;
                }
        }

        if (ImGui.Selectable("Define##entry"))
        {
            spelling.Define(word, word, replace);
            return true;
        }

        if (!misspelled)
            return false;

        ImGui.Separator();

        if (ImGui.Selectable("Add to dictionary##entry"))
        {
            spelling.AddToDictionary(word);
            return true;
        }

        if (ImGui.Selectable("Ignore for now##entry"))
        {
            Speller.Ignore(word);
            return true;
        }

        var corrections = shown.Corrections ??= spelling.Suggest(word);
        if (corrections is { Count: 0 })
            return false;

        ImGui.Separator();

        if (corrections is null)
        {
            ImGui.TextDisabled("Looking for corrections...");
            return false;
        }

        foreach (var correction in corrections)
            if (ImGui.Selectable(correction))
            {
                replace(correction);
                return true;
            }

        return false;
    }
}
