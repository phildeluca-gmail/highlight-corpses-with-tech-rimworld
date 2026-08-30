using System;
using System.Reflection;
using HighlightCorpsesWithTech.Core;
using UnityEngine;
using Verse;

namespace HighlightCorpsesWithTech.UI
{
    // Architecture 10.8 - the mod's single reflection point, and its single fragile
    // dependency.
    //
    // A corpse is not drawn from corpse.Graphic; it renders through its inner pawn's
    // PawnRenderer, so there is no public corpse-shaped mesh to re-draw. Vanilla does
    // cache a body-shaped silhouette for its own zoomed-out pawn rendering:
    //
    //   private static ValueTuple<Mesh, Material>
    //       Verse.SilhouetteUtility.GetCachedSilhouetteData(Thing thing)
    //
    // Verified present in 1.5.4409, returning exactly (Mesh, Material). We take the
    // geometry and supply our own material - vanilla's colour and alpha are not used.
    //
    // Plain System.Reflection, NOT HarmonyLib.AccessTools. Touching HarmonyLib would
    // make this mod fail to JIT when Harmony is absent, which is exactly the
    // dependency test T8.3 exists to prevent. Nothing here is a patch: no method is
    // intercepted, redirected or rewritten.
    public static class SilhouetteAccess
    {
        private const string TypeName = "Verse.SilhouetteUtility";
        private const string MethodName = "GetCachedSilhouetteData";

        private static MethodInfo method;
        private static bool resolveAttempted;
        private static bool warned;

        // False once the reflection target has been looked for and not found. The
        // outline component falls back to a plain halo in that case (10.8).
        public static bool Available
        {
            get
            {
                Resolve();
                return method != null;
            }
        }

        private static void Resolve()
        {
            if (resolveAttempted)
            {
                return;
            }

            resolveAttempted = true;

            Type type = typeof(Thing).Assembly.GetType(TypeName);
            if (type == null)
            {
                WarnOnce(TypeName + " not found");
                return;
            }

            method = type.GetMethod(MethodName, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                WarnOnce(TypeName + "." + MethodName + " not found");
                return;
            }

            HcwtLog.Message("silhouette reflection resolved: " + TypeName + "." + MethodName);
        }

        // One warning, unconditionally - not gated behind the verbose setting, and
        // never repeated. A reflection target that fails quietly is the failure that
        // has cost this project sessions before (architecture 10.8).
        private static void WarnOnce(string detail)
        {
            if (warned)
            {
                return;
            }

            warned = true;
            Log.Warning("[HighlightCorpsesWithTech] " + detail +
                " - corpse outlines fall back to a plain halo. Detection and the alert " +
                "are unaffected.");
        }

        // Returns vanilla's cached body silhouette geometry for this thing. The
        // material comes back too, but only its texture is used - the colour and
        // alpha are ours.
        public static bool TryGetSilhouette(Thing thing, out Mesh mesh, out Material sourceMaterial)
        {
            mesh = null;
            sourceMaterial = null;

            Resolve();
            if (method == null)
            {
                return false;
            }

            try
            {
                object result = method.Invoke(null, new object[] { thing });
                if (result == null)
                {
                    return false;
                }

                ValueTuple<Mesh, Material> data = (ValueTuple<Mesh, Material>)result;
                mesh = data.Item1;
                sourceMaterial = data.Item2;
                return mesh != null && sourceMaterial != null;
            }
            catch (Exception ex)
            {
                // A throw from inside vanilla's own cache is not something we can fix,
                // and re-throwing it every frame would bury the log. Degrade instead.
                method = null;
                WarnOnce(MethodName + " threw (" + ex.GetType().Name + ")");
                return false;
            }
        }
    }
}
