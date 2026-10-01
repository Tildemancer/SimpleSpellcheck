using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TildeTools.Modules.EmoteSplitter.Chat;

internal static unsafe class ChatSender
{
    internal static AtkComponentTextInput* ChatLogInput() =>
        Svc.GameGui.GetAddonByName("ChatLog") is { IsNull: false } chatLog ? ((AddonChatLog*)chatLog.Address)->TextInput : null;
}
