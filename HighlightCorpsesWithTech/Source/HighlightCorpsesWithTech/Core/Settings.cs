using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace HighlightCorpsesWithTech.Core
{
    // The mod's four settings (architecture 4, 10.5 and 10.9): which tech tiers count
    // as worth marking, whether your own dead are marked at all, whether the on-map
    // outline draws, and verbose logging.
    //
    // The colonist toggle was refused twice before it was ordered - architecture 10.1
    // closed the question as "include, no special case, no checkbox" - and then asked
    // for from play on 2026-08-30. It defaults to ON, which is the behaviour every
    // existing save already has; turning it off is one click and changes nothing
    // else.
    public class HcwtSettings : ModSettings
    {
        // Vanilla's TechLevel with Animal dropped - nothing implantable carries it -
        // and Undefined moved to the end as the catch-all for modded implants that
        // declare no tech level.
        //
        // Built from the enum rather than hardcoded (architecture 4) so a tier added
        // by another mod cannot fall through a gap. Test T2.1 checks this by reading
        // the source, because the settings window cannot tell you which it is.
        public static readonly List<TechLevel> OfferedTiers = BuildOfferedTiers();

        // "Industrial and up", which is the agreed default from the original ask.
        // Neolithic and Medieval are the peg-leg band and stay off; Undefined stays
        // off so a modded implant declaring no tier cannot flood a heavy modlist.
        //
        // The peg-leg exclusion is a CONSEQUENCE of this list and must stay that way.
        // Nothing in this mod may exclude peg legs by name or by def - architecture 4
        // is explicit that hardcoding it would make the setting a lie. Test T2.3.
        private static readonly TechLevel[] DefaultEnabledTiers =
        {
            TechLevel.Industrial, TechLevel.Spacer, TechLevel.Ultra, TechLevel.Archotech
        };

        public List<TechLevel> enabledTiers = DefaultEnabledTiers.ToList();

        // On by default: 10.1 shipped with your own dead treated like anyone else's,
        // so defaulting this off would silently change what an existing save shows.
        public bool markColonistCorpses = true;

        public bool showOutline = true;
        public bool verboseLogging;

        public bool TierEnabled(TechLevel tier)
        {
            return enabledTiers != null && enabledTiers.Contains(tier);
        }

        private static List<TechLevel> BuildOfferedTiers()
        {
            List<TechLevel> tiers = Enum.GetValues(typeof(TechLevel))
                .Cast<TechLevel>()
                .Where(tier => tier != TechLevel.Animal && tier != TechLevel.Undefined)
                .OrderBy(tier => (int)tier)
                .ToList();

            // Last, and labelled as a catch-all in the window.
            tiers.Add(TechLevel.Undefined);
            return tiers;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref enabledTiers, "enabledTiers", LookMode.Value);
            Scribe_Values.Look(ref markColonistCorpses, "markColonistCorpses", true);
            Scribe_Values.Look(ref showOutline, "showOutline", true);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);

            // A settings file written before this list existed, or hand-edited to
            // nothing, would otherwise leave a null here and throw on the first scan.
            if (Scribe.mode == LoadSaveMode.LoadingVars && enabledTiers == null)
            {
                enabledTiers = DefaultEnabledTiers.ToList();
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Mark corpses carrying tech of these tiers:");
            foreach (TechLevel tier in OfferedTiers)
            {
                bool on = TierEnabled(tier);
                bool was = on;
                listing.CheckboxLabeled(LabelFor(tier), ref on, TooltipFor(tier));
                if (on == was)
                {
                    continue;
                }

                if (on)
                {
                    enabledTiers.Add(tier);
                }
                else
                {
                    enabledTiers.Remove(tier);
                }
            }

            listing.Gap();
            listing.CheckboxLabeled("Mark your own dead", ref markColonistCorpses,
                "Colonists and slaves of your own faction. Off means a colonist who dies " +
                "with a bionic arm is left alone; everyone else's dead are unaffected " +
                "either way.");
            listing.CheckboxLabeled("Show the on-map outline", ref showOutline,
                "The pulsing outline over qualifying corpses. The alert has no toggle - " +
                "a mod that can be silenced entirely is just an uninstall.");
            listing.CheckboxLabeled("Verbose logging (for bug reports)", ref verboseLogging,
                "Writes what the scan found to the log. Off unless you are chasing something.");

            listing.End();
        }

        private static string LabelFor(TechLevel tier)
        {
            if (tier == TechLevel.Undefined)
            {
                return "Undefined (catch-all)";
            }

            return tier.ToString();
        }

        private static string TooltipFor(TechLevel tier)
        {
            switch (tier)
            {
                case TechLevel.Neolithic:
                    return "Peg legs, hook hands, wooden feet.";
                case TechLevel.Medieval:
                    return "Dentures and the like.";
                case TechLevel.Industrial:
                    return "Simple prosthetic arms and legs.";
                case TechLevel.Spacer:
                    return "Bionic parts, joywires, painstoppers.";
                case TechLevel.Ultra:
                    return "High-end implants.";
                case TechLevel.Archotech:
                    return "Archotech parts.";
                case TechLevel.Undefined:
                    return "Modded implants that declare no tech level. Off by default.";
                default:
                    return null;
            }
        }

        // Logged when the settings window closes, which is when the tier set is
        // actually written to disk. Test T2.3 reads this line.
        public string EnabledTiersDescription()
        {
            if (enabledTiers == null || enabledTiers.Count == 0)
            {
                return "none";
            }

            StringBuilder sb = new StringBuilder();
            foreach (TechLevel tier in OfferedTiers)
            {
                if (!TierEnabled(tier))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(tier);
            }

            return sb.ToString();
        }
    }
}
