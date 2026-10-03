using System;
using Dalamud.Bindings.ImGui;

namespace SimpleSpellcheck;

internal static class Widgets
{
    internal static void Open(string url)
    {
        try
        {
            Dalamud.Utility.Util.OpenLink(url);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Could not open {url}.");
        }
    }

    // Setters take what they write to as state, so callers pass static lambdas and nothing's allocated per frame. Blessings and all that.

    // Applied on release, so it doesn't save on every frame.
    internal static bool Paced<TState>(string label, ref int shown, int actual, int min, int max, TState state, Action<TState, int> apply)
    {
        if (shown < 0)
            shown = actual;

        ImGui.SliderInt(label, ref shown, min, max);

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            shown = Math.Clamp(shown, min, max);
            apply(state, shown);
            return true;
        }

        // Follows the setting again once released, so a value clamped elsewhere doesn't sit stale.
        if (!ImGui.IsItemActive())
            shown = actual;

        return false;
    }

    // Arguments run left to right, so Apply sees the value ImGui just wrote.
    internal static bool Toggle<TState>(string label, bool value, TState state, Action<TState, bool> set) =>
        Apply(ImGui.Checkbox(label, ref value), value, state, set);

    private static bool Apply<TState, T>(bool changed, T value, TState state, Action<TState, T> set)
    {
        if (changed)
            set(state, value);

        return changed;
    }
}
