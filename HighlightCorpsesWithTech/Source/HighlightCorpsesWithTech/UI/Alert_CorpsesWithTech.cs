using System.Collections.Generic;
using System.Text;
using HighlightCorpsesWithTech.Core;
using HighlightCorpsesWithTech.Detection;
using RimWorld;
using Verse;

namespace HighlightCorpsesWithTech.UI
{
    // The alert half of the mod (architecture 5.1).
    //
    // Vanilla's AlertsReadout discovers Alert subclasses by reflection at
    // construction, so SUBCLASSING IS THE WHOLE REGISTRATION - no def, no XML, no
    // Harmony patch. There is deliberately no Defs/ folder in this mod; a def
    // appearing would be the visible symptom of this having quietly failed and been
    // worked around (tests T3.1 and T8.4).
    //
    // The alert has no settings toggle. Architecture 4: a mod that can be silenced
    // entirely is just an uninstall.
    //
    // Since architecture section 15 (2026-09-26), a corpse the player has turned
    // OFF in the settings window's list is still scanned and still outlined, but
    // is left out of this alert - GetLabel, GetExplanation and GetReport all read
    // through ActiveCulprits(), which is the one place the toggle is applied.
    public class Alert_CorpsesWithTech : Alert
    {
        private readonly List<Thing> culprits = new List<Thing>();

        // Throttled per the logging standard: report the skip count only when
        // it changes, not on every alert refresh.
        private int lastLoggedSkipped = -1;

        public Alert_CorpsesWithTech()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel()
        {
            return culprits.Count == 1
                ? "1 corpse with tech"
                : culprits.Count + " corpses with tech";
        }

        // Architecture 5.1: "3 corpses with tech" without naming the archotech eye is
        // a prompt to go hunting manually. Each corpse is listed WITH what is on it,
        // and a corpse with several implants lists all of them (test T3.3).
        public override TaggedString GetExplanation()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("These corpses still carry recoverable tech:");
            sb.AppendLine();

            List<QualifiedCorpse> active = ActiveEntries(out _);
            for (int i = 0; i < active.Count; i++)
            {
                QualifiedCorpse entry = active[i];

                sb.Append("  ").Append(entry.corpse.LabelShortCap);

                // A corpse in a grave or container is counted and named, it just
                // has no outline (architecture 10.2).
                if (!entry.corpse.Spawned)
                {
                    sb.Append(" (stored)");
                }

                sb.Append(": ");

                for (int f = 0; f < entry.findings.Count; f++)
                {
                    if (f > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(entry.findings[f].yields.label);
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEndNewlines();
        }

        // AlertReport.CulpritsAre is the entire answer to "the corpse is off-screen",
        // which no on-map drawing can solve - clicking the alert cycles the camera
        // through them (architecture 5.1, test T3.2).
        //
        // Reads the per-map cache; never scans. If the two counts ever disagree with
        // the outlines on screen, they are not reading the same cache (test T3.5).
        public override AlertReport GetReport()
        {
            culprits.Clear();

            List<QualifiedCorpse> active = ActiveEntries(out int skipped);
            for (int i = 0; i < active.Count; i++)
            {
                culprits.Add(active[i].corpse);
            }

            // Fires only when the skipped count changes - the same throttle as
            // CorpseOutlineComponent's scan line (architecture 10.5/12.5).
            if (skipped != lastLoggedSkipped)
            {
                lastLoggedSkipped = skipped;
                if (skipped > 0)
                {
                    HcwtLog.Message("alert skipping " + skipped +
                        " corpse(s) toggled off in the settings window");
                }
            }

            return culprits.Count == 0 ? AlertReport.Inactive : AlertReport.CulpritsAre(culprits);
        }

        // Both GetExplanation and GetReport need the same filtered list: every
        // qualifying, non-destroyed corpse across every loaded map, minus any the
        // player has turned off (architecture section 15). One method, one place
        // the toggle is applied, so the two surfaces cannot disagree.
        private List<QualifiedCorpse> ActiveEntries(out int skippedCount)
        {
            List<QualifiedCorpse> result = new List<QualifiedCorpse>();
            skippedCount = 0;

            CorpseToggleComponent toggle = Current.Game == null
                ? null
                : Current.Game.GetComponent<CorpseToggleComponent>();

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                CorpseOutlineComponent component = maps[m].GetComponent<CorpseOutlineComponent>();
                if (component == null)
                {
                    continue;
                }

                List<QualifiedCorpse> qualified = component.Qualified;
                for (int i = 0; i < qualified.Count; i++)
                {
                    QualifiedCorpse entry = qualified[i];

                    // The cached list must tolerate entries that vanished since the
                    // last scan - butchered, destroyed, hauled away (architecture 7,
                    // tests T3.4 and T6.2).
                    if (entry.corpse == null || entry.corpse.Destroyed)
                    {
                        continue;
                    }

                    if (toggle != null && toggle.IsOff(entry.corpse))
                    {
                        skippedCount++;
                        continue;
                    }

                    result.Add(entry);
                }
            }

            return result;
        }
    }
}
