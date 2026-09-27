using System.Collections.Generic;
using Verse;

namespace HighlightCorpsesWithTech.Core
{
    // The per-save exclusion list for the alert (architecture section 15, ask of
    // 2026-09-26): which corpses the scanner qualifies but the player has turned
    // OFF. A corpse in this set is left out of Alert_CorpsesWithTech only - the
    // outline and the dialog-name colouring (UI/TransferableLabelPatch) read
    // nothing from here and are unchanged.
    //
    // This lives in the SAVE, not in HcwtSettings - "corpse #4821 in this save"
    // is not a mod-wide setting, and HcwtSettings is one file shared by every
    // save. Verse.GameComponent's own ExposeData is exactly the per-save slot.
    //
    // No registration code and no Harmony patch: verified by reading the IL
    // that Verse.Game's own constructor calls Game.FillComponents(), which adds
    // one instance of every non-abstract Verse.GameComponent subclass it does
    // not already have. See architecture 15.2.
    public class CorpseToggleComponent : GameComponent
    {
        // Keyed by Thing.thingIDNumber - a public int field on Verse.Thing,
        // stable for the life of the corpse and unaffected by settings changes.
        private HashSet<int> offIds = new HashSet<int>();

        // Every corpse ID a scan has examined - qualifying or not - since the
        // last prune round. Fed by CorpseOutlineComponent.Rescan(), which
        // already walks every corpse on its own interval (architecture section
        // 6); this adds no scanning of its own.
        private readonly HashSet<int> seenSinceLastPrune = new HashSet<int>();

        // 10x CorpseOutlineComponent's own 120-tick scan interval, so every
        // loaded map has scanned several times before a prune round runs.
        private const int PruneIntervalTicks = 1200;

        // Counted from when THIS component started ticking, not Find.TickManager's
        // raw game tick - a loaded save can resume at any tick count, and using
        // the raw tick risked a prune running before this session had scanned
        // anything at all, wiping a legitimately-off corpse on the very first
        // tick after load.
        private int ticksSinceStart;

        public CorpseToggleComponent(Game game)
        {
        }

        public bool IsOff(Corpse corpse)
        {
            return corpse != null && offIds.Contains(corpse.thingIDNumber);
        }

        // Logs every toggle, on or off - the logging standard (CLAUDE.md
        // section 4) asks for the event the mod caused, not a sample of it.
        public void SetOff(Corpse corpse, bool off)
        {
            if (corpse == null)
            {
                return;
            }

            bool was = offIds.Contains(corpse.thingIDNumber);
            if (off == was)
            {
                return;
            }

            if (off)
            {
                offIds.Add(corpse.thingIDNumber);
            }
            else
            {
                offIds.Remove(corpse.thingIDNumber);
            }

            HcwtLog.Message((off ? "toggle off " : "toggle on ") + corpse.LabelShort +
                " (id " + corpse.thingIDNumber + ") - " +
                (off ? "excluded from" : "restored to") + " the alert");
        }

        // Called once per map per scan by CorpseOutlineComponent.Rescan() with
        // the ID of every corpse it examined, qualifying or not. "Examined" is
        // the honest test for "still exists" - a corpse that stops QUALIFYING
        // because a tier setting changed must not be mistaken for one that no
        // longer exists (test T15.5).
        public void NoteExamined(List<int> examinedIds)
        {
            for (int i = 0; i < examinedIds.Count; i++)
            {
                seenSinceLastPrune.Add(examinedIds[i]);
            }
        }

        public override void GameComponentTick()
        {
            ticksSinceStart++;
            if (ticksSinceStart % PruneIntervalTicks != 0)
            {
                return;
            }

            // Never prune on an empty observation window - it means no map has
            // scanned yet this round, not that every corpse is gone.
            if (offIds.Count == 0 || seenSinceLastPrune.Count == 0)
            {
                seenSinceLastPrune.Clear();
                return;
            }

            int before = offIds.Count;
            offIds.RemoveWhere(id => !seenSinceLastPrune.Contains(id));
            seenSinceLastPrune.Clear();

            int removed = before - offIds.Count;
            if (removed > 0)
            {
                HcwtLog.Message("pruned " + removed +
                    " off-toggle id(s) for corpses no longer found");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref offIds, "offCorpseIds", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars && offIds == null)
            {
                offIds = new HashSet<int>();
            }
        }
    }
}
