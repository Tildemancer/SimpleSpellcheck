using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Lumina.Excel.Sheets;

namespace SimpleSpellcheck;

public sealed class Plugin : IDalamudPlugin
{
    private static readonly string[] CommandNames = ["/spellcheck", "/ss", "/scheck", "/spelling", "/sspell", "/spell"];

    private readonly Configuration _config;
    private readonly WindowSystem _windows = new("SimpleSpellcheck");
    private readonly DefineWindow _define;
    private readonly Spelling _spelling;
    private readonly NativeChatSpelling _native;
    private readonly SettingsWindow _settings;
    private readonly List<string> _commands = [];

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

        foreach (var name in CommandNames)
        {
            var info = new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Simple Spellcheck\n/spellcheck define - Look a word up\n/spellcheck define <word> - Define that word",
                ShowInHelp = name == CommandNames[0],
            };

            if (Svc.Commands.AddHandler(name, info))
                _commands.Add(name);
            else
                Svc.Log.Warning($"{name} is already another plugin's command.");
        }
    }

    private void Save() => Svc.Pi.SavePluginConfig(_config);

    private void OnCommand(string command, string args)
    {
        var parts = args.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);

        if (!parts[0].Equals("define", StringComparison.OrdinalIgnoreCase))
            _settings.Toggle();
        else if (parts is [_, { Length: > 0 } word])
            _define.Open(word, word, null);
        else
            _define.OpenSearch();
    }

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
        foreach (var name in _commands)
            Svc.Commands.RemoveHandler(name);

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
