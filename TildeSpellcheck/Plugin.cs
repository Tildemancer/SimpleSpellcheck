using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Lumina.Excel.Sheets;

namespace TildeSpellcheck;

public sealed class Plugin : IDalamudPlugin
{
    private readonly Configuration _config;
    private readonly WindowSystem _windows = new("TildeSpellcheck");
    private readonly DefineWindow _define;
    private readonly Spelling _spelling;
    private readonly NativeChatSpelling _native;
    private readonly SettingsWindow _settings;

    private static string Dictionaries => Path.Combine(Svc.Pi.AssemblyLocation.DirectoryName!, "Dictionaries");

    private static string LexiconFolder => Path.Combine(Svc.Pi.AssemblyLocation.DirectoryName!, "Lexicon");

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Svc>();

        ChannelCommands.AddClientNames(Svc.Data.GetExcelSheet<TextCommand>()
            .Select(c => new[] { c.Command, c.ShortCommand, c.Alias, c.ShortAlias }.Select(n => n.ExtractText())));

        _config = Svc.Pi.GetPluginConfig() as Configuration ?? new Configuration();

        _define = new DefineWindow(_config, LexiconFolder);
        _spelling = new Spelling(_config, Save, LexiconFolder, _define.Open);
        _native = new NativeChatSpelling(_spelling);
        _settings = new SettingsWindow(_config, Save, () => _ = Load());
        _spelling.Changed += _native.Recheck;

        _windows.AddWindow(_define);
        _windows.AddWindow(_native.Menu);
        _windows.AddWindow(_settings);

        _ = Load();
        GameVocabulary.Supply();

        // Before _windows.Draw, which reads the _menuWanted this sets.
        Svc.Pi.UiBuilder.Draw += _native.Draw;
        Svc.Pi.UiBuilder.Draw += _windows.Draw;
        Svc.Pi.UiBuilder.OpenConfigUi += _settings.Toggle;
        Svc.Pi.UiBuilder.OpenMainUi += _define.OpenSearch;
    }

    private void Save() => Svc.Pi.SavePluginConfig(_config);

    private async Task Load()
    {
        try
        {
            var terms = Lexicon.Terms(LexiconFolder).SelectMany(term => term.Spellings);

            if (await Speller.Load(Dictionaries, _config.British, _config.CustomWords, terms) is { } loaded)
                Svc.Log.Info(loaded);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Could not load the dictionaries from {Dictionaries}.");
        }
    }

    public void Dispose()
    {
        Svc.Pi.UiBuilder.Draw -= _native.Draw;
        Svc.Pi.UiBuilder.Draw -= _windows.Draw;
        Svc.Pi.UiBuilder.OpenConfigUi -= _settings.Toggle;
        Svc.Pi.UiBuilder.OpenMainUi -= _define.OpenSearch;

        _spelling.Changed -= _native.Recheck;
        _windows.RemoveAllWindows();
        _spelling.Dispose();
        GameVocabulary.Forget();
        Speller.Unload();
    }
}
