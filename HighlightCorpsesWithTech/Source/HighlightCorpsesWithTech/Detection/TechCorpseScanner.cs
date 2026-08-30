using System.Collections.Generic;
using HighlightCorpsesWithTech.Core;
using RimWorld;
using Verse;

namespace HighlightCorpsesWithTech.Detection
{
    // One qualifying implant on a corpse: the hediff, the item it leaves behind when
    // removed, and that item's tech level.
    public struct TechFinding
    {
        public HediffDef hediff;
        public ThingDef yields;
        public TechLevel tier;
    }

    // The architecture section 3 rule and nothing else. Pure: no caching, no drawing,
    // no side effects. The cache lives in CorpseOutlineComponent; both the outline
    // and the alert read it from there.
    public static class TechCorpseScanner
    {
        // A corpse qualifies when ANY hediff on its inner pawn satisfies all three:
        //
        //   1. def.countsAsAddedPartOrImplant - vanilla's own flag for "artificial
        //      part or implant", so we are not inventing a definition of tech.
        //   2. def.spawnThingOnRemoved != null - the part leaves a real item behind.
        //      LOAD-BEARING: something that vanishes on removal is nothing to haul a
        //      body for, whatever tier it claims. Test T1.4.
        //   3. spawnThingOnRemoved.techLevel is in the player's enabled tier set.
        //
        // The tier is read off the spawned ITEM, never off the hediff - HediffDef has
        // no tech level, and the item is what the player is actually recovering
        // (architecture 3). Test T1.6.
        //
        // One thing sits outside that rule: a corpse that would otherwise qualify is
        // dropped when it is one of the player's own and markColonistCorpses is off
        // (architecture 10.9, which reopens 10.1). Checked AFTER the hediff walk on
        // purpose - a colonist corpse with nothing implanted in it is not interesting
        // and must not produce a reject line, and the faction test then costs one
        // check per qualifying corpse rather than one per corpse on the map.
        //
        // Nothing here is a hardcoded def list, so any modded implant following the
        // vanilla pattern is detected without a compatibility patch. Test T1.5.
        public static bool TryQualify(Corpse corpse, HcwtSettings settings,
            List<TechFinding> findings, out string rejectReason)
        {
            findings.Clear();
            rejectReason = null;

            if (corpse == null || corpse.Destroyed)
            {
                rejectReason = "gone";
                return false;
            }

            // Verse.Corpse has this property for a reason. This mod touches InnerPawn
            // on every corpse on the map on an interval, which is the highest-exposure
            // place a null could land (architecture 7, test T6.1).
            if (corpse.Bugged)
            {
                rejectReason = "bugged";
                return false;
            }

            Pawn pawn = corpse.InnerPawn;
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                rejectReason = "bugged";
                return false;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            if (hediffs == null)
            {
                rejectReason = "bugged";
                return false;
            }

            bool sawImplant = false;
            string lastRejection = null;

            for (int i = 0; i < hediffs.Count; i++)
            {
                HediffDef def = hediffs[i] == null ? null : hediffs[i].def;
                if (def == null || !def.countsAsAddedPartOrImplant)
                {
                    continue;
                }

                sawImplant = true;

                ThingDef yields = def.spawnThingOnRemoved;
                if (yields == null)
                {
                    lastRejection = "no spawnThingOnRemoved";
                    continue;
                }

                if (!settings.TierEnabled(yields.techLevel))
                {
                    lastRejection = "tier " + yields.techLevel + " disabled";
                    continue;
                }

                findings.Add(new TechFinding
                {
                    hediff = def,
                    yields = yields,
                    tier = yields.techLevel
                });
            }

            if (findings.Count > 0)
            {
                if (!settings.markColonistCorpses && IsPlayersOwnDead(pawn))
                {
                    findings.Clear();
                    rejectReason = "your own dead, and marking them is off";
                    return false;
                }

                return true;
            }

            // Only worth a reason if there was something to reject. A corpse with no
            // added parts at all is not interesting and should not be logged.
            rejectReason = sawImplant ? lastRejection : null;
            return false;
        }

        // "Your own dead" is humanlike and of the player's faction, which is
        // colonists and your own slaves. Deliberately not Pawn.IsColonist: that is
        // false for a slave, and a dead slave is still one of yours to bury rather
        // than a body to point at. Deliberately not faction alone either - a dead
        // colony animal with an implant is not what the checkbox is about.
        //
        // Faction survives death in RimWorld, so this reads the same after the pawn
        // dies as before.
        private static bool IsPlayersOwnDead(Pawn pawn)
        {
            return pawn.RaceProps != null
                && pawn.RaceProps.Humanlike
                && pawn.Faction != null
                && pawn.Faction.IsPlayer;
        }

        // Architecture 7: rank by highest tier. Since 2026-08-30 that drives the
        // outline's brightness rather than its hue (10.4).
        public static TechLevel HighestTier(List<TechFinding> findings)
        {
            TechLevel best = TechLevel.Undefined;
            for (int i = 0; i < findings.Count; i++)
            {
                if ((int)findings[i].tier > (int)best)
                {
                    best = findings[i].tier;
                }
            }

            return best;
        }
    }
}
