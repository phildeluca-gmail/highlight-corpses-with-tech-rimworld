using Verse;

namespace HighlightCorpsesWithTech.Core
{
    // The architecture 10.5 trace.
    //
    // Message() is silent unless the verbose setting is on. Warning() always speaks,
    // because the things it reports - a reflection target that did not resolve, most
    // of all - are what make every other line in the log meaningless.
    //
    // Do Not Be Lazy shipped a Logger.Message that was a no-op for six weeks, and
    // several playtests were misread as "nothing fired". Test T0.2 exists to prove
    // this one speaks before any result is trusted.
    //
    // **Changed 2026-09-06: Message no longer touches Verse.Log.** It writes to
    // HighlightCorpsesWithTech.log instead - see Core/LogFile. This mod needed it
    // more than it looked: the 2026-09-06 log carried 792 copies of one per-scan
    // line, 40% of everything RimWorld wrote that session, from a mod nobody was
    // debugging. Ordered the same day - "each mod should have a log."
    //
    // **The per-scan line is still a defect and moving it has not fixed it.** The
    // standing rule - a line that can fire per target, per def or per tick is a
    // defect until proved otherwise - is about traces nobody can read. Writing 792
    // of them somewhere cheaper makes them cheap, not useful.
    public static class HcwtLog
    {
        private const string Prefix = "[HighlightCorpsesWithTech] ";

        public static void Message(string text)
        {
            HcwtSettings settings = HighlightCorpsesWithTechMod.Settings;
            if (settings == null || !settings.verboseLogging)
            {
                return;
            }

            if (LogFile.Available)
            {
                LogFile.Write("MSG ", text);
                return;
            }

            // The file could not be opened. Better to spend RimWorld's message
            // cap than to hand back an empty verbose session.
            Log.Message(Prefix + text);
        }

        // Never gated behind the verbose setting, and written to both places -
        // the file so this mod's log is complete on its own, Verse.Log so a
        // broken mod is visible in-game.
        public static void Warning(string text)
        {
            LogFile.Write("WARN", text);
            Log.Warning(Prefix + text);
        }
    }
}
