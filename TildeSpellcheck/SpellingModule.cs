using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using static TildeTools.Ui.Widgets;

namespace TildeTools.Modules.Spelling;

internal sealed class SpellingModule(SpellingSettings settings, Action save, WindowSystem windows)
{
    // Null while off
    internal SpellIpc? Ipc { get; private set; }

    private readonly DefineWindow _define = new(settings, LexiconFolder);

    public bool IsEnabled { get; private set; }

    private static string Dictionaries => Path.Combine(Svc.Pi.AssemblyLocation.DirectoryName!, "Dictionaries");

    private static string LexiconFolder => Path.Combine(Svc.Pi.AssemblyLocation.DirectoryName!, "Lexicon");

    public void Enable()
    {
        IsEnabled = true;

        _ = Load();
        Ipc = new SpellIpc(settings, save, LexiconFolder, _define.Open);

        if (!windows.Windows.Contains(_define))
            windows.AddWindow(_define);
        GameVocabulary.Supply();
    }

    private async Task Load()
    {
        try
        {
            var terms = Lexicon.Terms(LexiconFolder).SelectMany(term => term.Spellings);

            if (await Speller.Load(Dictionaries, settings.British, settings.CustomWords, terms) is { } loaded)
                Svc.Log.Info(loaded);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Could not load the dictionaries from {Dictionaries}.");
        }
    }

    public void Disable()
    {
        GameVocabulary.Forget();
        _define.IsOpen = false;
        Ipc?.Dispose();
        Ipc = null;
        Speller.Unload();
        IsEnabled = false;
    }

    private int _suggestions = -1;

    public void DrawTab()
    {
        using var wrap = ImRaii.TextWrapPos(0f);

        ImGui.TextUnformatted("Dictionary");

        if (Toggle("British spelling first", settings.British, settings, static (s, v) => s.British = v))
        {
            save();
            _ = Load();
        }

        ImGui.TextDisabled("Both are always accepted: this picks whose corrections come first.");

        if (Paced("Corrections to offer", ref _suggestions, settings.MaximumSuggestions, 1, SpellingSettings.MostSuggestions, settings, static (s, v) => s.MaximumSuggestions = v))
            save();

        if (Toggle("Skip a word cut off with a hyphen", settings.IgnoreWordsEndingInHyphen, settings, static (s, v) => s.IgnoreWordsEndingInHyphen = v))
            save();

        ImGui.TextDisabled("\"I was going to-\" isn't an actual typo.");

        ImGui.Separator();
        ImGui.TextUnformatted("Define and synonyms");
        ImGui.TextDisabled("Right-click any word in the chat box.");

        if (Toggle("Look a word up online when the bundled definitions lack it", settings.LookUpOnline, settings, static (s, v) => s.LookUpOnline = v))
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

    public void Dispose()
    {
        if (IsEnabled)
            Disable();
    }
}
