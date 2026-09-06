using HarmonyLib;
using UnityEngine;
using Verse;

namespace HighlightCorpsesWithTech.Core
{
    // The mod entry point. RimWorld constructs one of these per load, passing the
    // ModContentPack.
    //
    // **This mod had no Harmony patches until 2026-09-06, and that was a stated
    // invariant guarded by test T8.3.** It has exactly one now:
    // UI/TransferableLabelPatch, which colours a qualifying corpse's NAME in the
    // dialogs it appears in - the load cargo dialog above all. Ordered
    // 2026-09-06; see architecture section 12.
    //
    // Everything else is unchanged and still needs no patch: detection is vanilla
    // API, the alert registers itself by subclassing RimWorld.Alert, and the
    // outline is drawn by a MapComponent.
    //
    // **The cost, paid deliberately:** About.xml now declares brrainz.harmony,
    // where before it declared nothing at all. Players get Harmony as an ordinary
    // Workshop subscription, the same as every other mod in this project.
    public class HighlightCorpsesWithTechMod : Mod
    {
        public const string HarmonyId = "phildeluca.highlightcorpseswithtech";

        public static HcwtSettings Settings;

        public HighlightCorpsesWithTechMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HcwtSettings>();

            // PatchAll is enough here: the one patch targets a vanilla type this
            // assembly references directly, so there is nothing to resolve by
            // name. Standard Cargo needs a hand-applied patch for the opposite
            // reason - see its VehicleGizmoPatch.
            new Harmony(HarmonyId).PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Highlight Corpses With Tech";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        // RimWorld calls this when the settings window closes - which is the moment
        // the tier set reaches disk, and the moment the scan's answer can change.
        public override void WriteSettings()
        {
            base.WriteSettings();
            HcwtLog.Message("tiers enabled: " + Settings.EnabledTiersDescription());
        }
    }
}
