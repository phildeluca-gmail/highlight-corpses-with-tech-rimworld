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

            Log.Message(Prefix + text);
        }

        // Never gated behind the verbose setting.
        public static void Warning(string text)
        {
            Log.Warning(Prefix + text);
        }
    }
}
