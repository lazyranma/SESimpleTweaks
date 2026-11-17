using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SimpleTweaks
{
    /// <summary>
    /// Overrides <see cref="ShowToolTip"/> text without losing the vanilla state. A component
    /// the plugin adds is destroyed on teardown; for a component that already existed, only
    /// the fields this plugin actually writes are recorded and restored.
    /// </summary>
    internal static class TooltipHook
    {
        private sealed class Original
        {
            internal bool Listed;
            internal bool TextTracked;
            internal string Text;
            internal bool RefreshTracked;
            internal Func<(string, List<(string, string)>, string)> Refresh;
        }

        private static readonly ConditionalWeakTable<ShowToolTip, object> Owned =
            new ConditionalWeakTable<ShowToolTip, object>();
        private static readonly ConditionalWeakTable<ShowToolTip, Original> Originals =
            new ConditionalWeakTable<ShowToolTip, Original>();
        private static readonly LiveObjectList<ShowToolTip> Tracked = new LiveObjectList<ShowToolTip>();

        /// <summary>
        /// Returns the tooltip on <paramref name="host"/>, adding one when the game does not
        /// provide it. A component added this way is removed on teardown; a game-owned one is
        /// left in place and only its overridden fields are restored.
        /// </summary>
        internal static ShowToolTip Acquire(GameObject host)
        {
            ShowToolTip tooltip = host.GetComponent<ShowToolTip>();
            if (tooltip == null)
            {
                tooltip = host.AddComponent<ShowToolTip>();
                Owned.GetOrCreateValue(tooltip);
                Teardown.TrackAddedComponent(tooltip);
            }
            return tooltip;
        }

        /// <summary>Sets custom text and restores the previous text on teardown.</summary>
        internal static void SetText(ShowToolTip tooltip, string text)
        {
            if (tooltip == null) return;

            Original original = Record(tooltip);
            if (original != null && !original.TextTracked)
            {
                original.TextTracked = true;
                original.Text = tooltip.CustomTextFromCode;
            }
            tooltip.CustomTextFromCode = text;
        }

        /// <summary>Sets the refresh delegate and restores the previous one on teardown.</summary>
        internal static void SetRefresh(
            ShowToolTip tooltip,
            Func<(string, List<(string, string)>, string)> refresh)
        {
            if (tooltip == null) return;

            Original original = Record(tooltip);
            if (original != null && !original.RefreshTracked)
            {
                original.RefreshTracked = true;
                original.Refresh = tooltip.CustomTextFromCodeRefreshText2;
            }
            tooltip.CustomTextFromCodeRefreshText2 = refresh;
        }

        private static Original Record(ShowToolTip tooltip)
        {
            // Components added by the plugin are destroyed, so they need no restore record.
            if (Owned.TryGetValue(tooltip, out _)) return null;

            Original original = Originals.GetValue(tooltip, _ => new Original());
            if (!original.Listed)
            {
                original.Listed = true;
                Tracked.Add(tooltip);
            }
            return original;
        }

        /// <summary>Restores the overridden fields still reachable at teardown.</summary>
        internal static void RestoreAll()
        {
            Tracked.Visit(tooltip =>
            {
                if (!Originals.TryGetValue(tooltip, out Original original)) return;

                if (original.TextTracked) tooltip.CustomTextFromCode = original.Text;
                if (original.RefreshTracked) tooltip.CustomTextFromCodeRefreshText2 = original.Refresh;
            });
        }
    }
}
