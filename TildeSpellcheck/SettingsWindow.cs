using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace TildeSpellcheck;

internal sealed class SettingsWindow(Configuration settings, Action save, Action reload) : Window("TildeSpellcheck###tildespellcheck-settings")
{
    private int _suggestions = -1;

    public override void Draw()
    {
        using var wrap = ImRaii.TextWrapPos(0f);

        ImGui.TextUnformatted("Dictionary");

        if (Widgets.Toggle("British spelling first", settings.British, settings, static (s, v) => s.British = v))
        {
            save();
            reload();
        }

        ImGui.TextDisabled("Both are always accepted: this picks whose corrections come first.");

        if (Widgets.Paced("Corrections to offer", ref _suggestions, settings.MaximumSuggestions, 1, Configuration.MostSuggestions, settings, static (s, v) => s.MaximumSuggestions = v))
            save();

        if (Widgets.Toggle("Skip a word cut off with a hyphen", settings.IgnoreWordsEndingInHyphen, settings, static (s, v) => s.IgnoreWordsEndingInHyphen = v))
            save();

        ImGui.TextDisabled("\"I was going to-\" isn't an actual typo.");

        ImGui.Separator();
        ImGui.TextUnformatted("Define and synonyms");
        ImGui.TextDisabled("Right-click any word in the chat box.");

        if (Widgets.Toggle("Look a word up online when the bundled definitions lack it", settings.LookUpOnline, settings, static (s, v) => s.LookUpOnline = v))
            save();

        ImGui.TextDisabled("Define can make a network call to Wiktionary, if it can't define a word locally.");

        ImGui.Separator();
        DrawOwnWords();
    }

    private void DrawOwnWords()
    {
        ImGui.TextUnformatted($"Your words ({settings.CustomWords.Count})");
        ImGui.TextDisabled("Words you've added to your dictionary.");

        using var list = ImRaii.Child("##spelling-words", new(0, ImGui.GetTextLineHeightWithSpacing() * Math.Min(8, settings.CustomWords.Count + 1)), true);
        if (!list.Success)
            return;

        string? removed = null;

        // Only the rows in view! Long word lists and short word lists are identical in performance cost.
        // I love performance
        ImGuiClip.ClippedDraw(settings.CustomWords, word =>
        {
            using var id = ImRaii.PushId(word);

            if (ImGui.SmallButton("x"))
                removed = word;

            ImGui.SameLine();
            ImGui.TextUnformatted(word);
        }, 1, ImGui.GetTextLineHeightWithSpacing());

        if (removed is null)
            return;

        _ = settings.CustomWords.Remove(removed);
        Speller.RemoveWord(removed);
        save();
    }
}
