using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace TildeSpellcheck;

internal sealed class Svc
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
}
