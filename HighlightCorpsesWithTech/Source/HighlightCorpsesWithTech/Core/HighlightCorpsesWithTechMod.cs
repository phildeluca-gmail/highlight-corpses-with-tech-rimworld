using UnityEngine;
using Verse;

namespace HighlightCorpsesWithTech.Core
{
    // The mod entry point. RimWorld constructs one of these per load, passing the
    // ModContentPack.
    //
    // There are no Harmony patches anywhere in this mod, and no dependency on
    // Harmony is declared in About.xml. Detection is vanilla API; the alert
    // registers itself by subclassing RimWorld.Alert; the outline is drawn by a
    // MapComponent. See architecture section 5, and test T8.3 which guards it.
    public class HighlightCorpsesWithTechMod : Mod
    {
        public static HcwtSettings Settings;

        public HighlightCorpsesWithTechMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HcwtSettings>();
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
