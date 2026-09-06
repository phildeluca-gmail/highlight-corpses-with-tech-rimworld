using System;
using System.IO;
using System.Text;
using Verse;

namespace HighlightCorpsesWithTech.Core
{
    // A log file of this mod's own, beside RimWorld's `Player.log`.
    //
    // WHY THIS EXISTS. `Verse.Log` stops at 1000 messages and then sets
    // `Debug.unityLogger.logEnabled = false` - read off
    // `Verse.Log.Notify_MessageReceivedThreadedInternal` in
    // `lib\Assembly-CSharp.dll` on 2026-09-06. That switch is global: it
    // silences RimWorld and every other mod, not just the one that spent the
    // budget. This project has lost the evidence for a bug being chased to
    // that cap in three separate sessions.
    //
    // The cap counts MESSAGES, not characters, and it only counts what goes
    // through Unity's logger. Writing to our own file costs nothing from the
    // shared budget, so a verbose trace can be as long as it needs to be
    // without silencing the game.
    //
    // Ordered 2026-09-06: "just do that. It's what I wanted."
    //
    // The division of labour:
    //   - Message  -> this file ONLY. Never touches Verse.Log, so a verbose
    //                 trace can never blow the cap for anybody.
    //   - Warning  -> this file AND Verse.Log. A silhouette reflection target that did not
    //                 resolve has to be visible in-game, and there are few
    //                 enough of them that the cap is not at risk.
    //   - Error    -> the same.
    //
    // Nothing here may throw. A logger that takes the game down is worse than
    // no logger, so every path is wrapped and a failure degrades to
    // Verse.Log with one warning and never tries again.
    public static class LogFile
    {
        private const string FileName = "HighlightCorpsesWithTech.log";
        private const string PrevFileName = "HighlightCorpsesWithTech-prev.log";

        private static readonly object FileLock = new object();

        private static StreamWriter writer;
        private static bool tried;
        private static bool broken;

        public static string Path { get; private set; }

        // Rotates last session's file to -prev and opens a fresh one, exactly
        // the way RimWorld treats Player.log / Player-prev.log. One previous
        // session is kept because comparing against it is half the value of
        // having a log at all.
        private static void Open()
        {
            if (tried)
            {
                return;
            }

            tried = true;

            try
            {
                string folder = GenFilePaths.SaveDataFolderPath;
                string path = System.IO.Path.Combine(folder, FileName);
                string prev = System.IO.Path.Combine(folder, PrevFileName);

                if (File.Exists(path))
                {
                    if (File.Exists(prev))
                    {
                        File.Delete(prev);
                    }

                    File.Move(path, prev);
                }

                writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true };
                Path = path;

                writer.WriteLine("=== Highlight Corpses With Tech - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            }
            catch (Exception e)
            {
                broken = true;
                writer = null;
                Verse.Log.Warning("[HighlightCorpsesWithTech] could not open its own log file, falling back to Player.log: " + e.Message);
            }
        }

        public static void Write(string level, string text)
        {
            lock (FileLock)
            {
                Open();
                if (broken || writer == null)
                {
                    return;
                }

                try
                {
                    writer.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + level + "  " + text);
                }
                catch (Exception e)
                {
                    broken = true;
                    writer = null;
                    Verse.Log.Warning("[HighlightCorpsesWithTech] lost its own log file, falling back to Player.log: " + e.Message);
                }
            }
        }

        // Whether Message() has anywhere to go. When the file could not be
        // opened, Logger.Message falls back to Verse.Log so a verbose session
        // is not silently empty - it just costs the cap again.
        public static bool Available
        {
            get
            {
                lock (FileLock)
                {
                    Open();
                    return !broken && writer != null;
                }
            }
        }
    }
}
