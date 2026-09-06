using System.Collections.Generic;
using HarmonyLib;
using HighlightCorpsesWithTech.Core;
using HighlightCorpsesWithTech.Detection;
using RimWorld;
using UnityEngine;
using Verse;

namespace HighlightCorpsesWithTech.UI
{
    // Patches RimWorld.TransferableUIUtility.DrawTransferableInfo so a qualifying
    // corpse's NAME is drawn in the same pulsing blue as its on-map outline.
    //
    // Ordered 2026-09-06: "The name should be highlighted in the various dialogs
    // in which corpses appear, especially the load cargo dialog. That is
    // essential; the other similar dialogs are nices."
    //
    // WHY THIS ONE METHOD. It is the single place a transferable row's label is
    // drawn, it is public and static, and it takes the colour as a PARAMETER -
    // so a prefix changes the colour and nothing else. Verified against
    // lib\Assembly-CSharp.dll on 2026-09-06:
    //
    //   public static void DrawTransferableInfo(Transferable trad, Rect idRect, Color labelColor)
    //
    // Every dialog that lists transferables routes through it:
    //
    //   TransferableOneWayWidget.DoRow  -> Vehicles.Dialog_LoadCargo   <- THE ESSENTIAL ONE
    //                                   -> Dialog_LoadTransporters
    //                                   -> Dialog_EnterPortal
    //                                   -> CaravanUIUtility
    //   TradeUI.DrawTradeableRow        -> the trade dialog
    //
    // **Vehicle Framework's dialog is covered without naming a single VF type**,
    // because VF builds a plain vanilla TransferableOneWayWidget. That is why
    // this mod still declares no dependency on Vehicle Framework.
    [HarmonyPatch(typeof(TransferableUIUtility), nameof(TransferableUIUtility.DrawTransferableInfo))]
    public static class TransferableLabelPatch
    {
        // Answers are cached per corpse and only for the frame being drawn.
        // TryQualify walks every hediff on the inner pawn, and a prefix runs once
        // per visible row per frame - a scrolling cargo dialog would otherwise do
        // that walk sixty times a second per row. Architecture 12.2.
        private static readonly Dictionary<Corpse, bool> Cache = new Dictionary<Corpse, bool>();
        private static int cacheFrame = -1;

        // Reused so the qualify call allocates nothing per row. TryQualify clears
        // it on entry, and nothing here reads the findings back - only the bool
        // matters for a colour.
        private static readonly List<TechFinding> ScratchFindings = new List<TechFinding>();

        public static void Prefix(Transferable trad, ref Color labelColor)
        {
            if (trad == null || !trad.HasAnyThing)
            {
                return;
            }

            // A corpse inside a transferable is a plain Thing on the row. Anything
            // that is not one leaves the colour exactly as the caller set it -
            // including the slave-name colour TransferableOneWayWidget passes, and
            // the "trader will not take this" grey from TradeUI.
            if (!(trad.AnyThing is Corpse corpse))
            {
                return;
            }

            if (!Qualifies(corpse))
            {
                return;
            }

            labelColor = CorpseOutlineComponent.PulsedLabelColor();
        }

        private static bool Qualifies(Corpse corpse)
        {
            HcwtSettings settings = HighlightCorpsesWithTechMod.Settings;
            if (settings == null)
            {
                return false;
            }

            // Time.frameCount rather than a tick: this runs in OnGUI, which is
            // driven by frames and keeps running while the game is paused.
            int frame = Time.frameCount;
            if (cacheFrame != frame)
            {
                cacheFrame = frame;
                Cache.Clear();
            }

            if (Cache.TryGetValue(corpse, out bool cached))
            {
                return cached;
            }

            bool result = TechCorpseScanner.TryQualify(corpse, settings, ScratchFindings, out _);
            Cache[corpse] = result;
            return result;
        }
    }
}
