using System.Collections.Generic;
using System.Diagnostics;
using HighlightCorpsesWithTech.Core;
using HighlightCorpsesWithTech.Detection;
using RimWorld;
using UnityEngine;
using Verse;

namespace HighlightCorpsesWithTech.UI
{
    // One qualifying corpse and what is on it. Held in the per-map cache below and
    // read by both surfaces - the outline here and Alert_CorpsesWithTech.
    public class QualifiedCorpse
    {
        public Corpse corpse;
        public List<TechFinding> findings = new List<TechFinding>();
    }

    // Scans on an interval and draws the architecture 10.3 outline.
    //
    // Both surfaces read this one cache (architecture 6). The alert does not scan and
    // the draw path does not scan - a per-frame hediff walk in a 60-mod game with
    // RocketMan throttling ticks is exactly the thing that shows up in somebody's
    // performance report later. Tests T3.5 and T7.1.
    public class CorpseOutlineComponent : MapComponent
    {
        private const int ScanIntervalTicks = 120;

        // What the last logged scan said. A scan whose numbers match the last one
        // reported says nothing new, so it says nothing at all - see the guard in
        // the scan itself. -1 so the first scan of a session always reports.
        private int lastReportedQualifying = -1;
        private int lastReportedExamined = -1;

        // One pulse per second, ordered 2026-08-30 (was a two-second cycle, 0.5).
        // Pulser.PulseBrightness takes the frequency in cycles per second, so
        // 1/second is literally 1f.
        private const float PulseFrequency = 1f;
        private const float PulseAmplitude = 0.6f;

        // The band the pulse swings the outline's alpha through. It starts high and
        // ends at full: "always bright blue" means the dim half of the old cycle is
        // gone, and what is left is a pulse you can see rather than one that fades
        // the corpse out. Was a per-tier floor with a 0.18 band above it.
        private const float PulseAlphaFloor = 0.65f;
        private const float PulseAlphaCeiling = 1f;

        // How far the silhouette is enlarged to show as a band around the corpse.
        private const float OutlineScale = 1.18f;
        private const float HaloScale = 1.75f;

        // Bright blue, and the same for every corpse (architecture 10.3/10.4 as
        // amended 2026-08-30). Was a paler (0.45, 0.75, 1) with the tech tier driving
        // brightness; tier no longer touches the outline at all and lives only in the
        // alert.
        private static readonly Color OutlineColor = new Color(0.25f, 0.6f, 1f);

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private readonly List<QualifiedCorpse> qualified = new List<QualifiedCorpse>();
        private readonly List<Thing> scratchThings = new List<Thing>();
        private readonly List<TechFinding> scratchFindings = new List<TechFinding>();
        private readonly HashSet<Corpse> previouslyQualifying = new HashSet<Corpse>();

        // Corpses already reported as having no silhouette. Keeps the halo
        // notice to one line per corpse rather than one per frame.
        private readonly HashSet<Corpse> reportedNoSilhouette = new HashSet<Corpse>();

        private Material haloMaterial;

        public CorpseOutlineComponent(Map map) : base(map)
        {
        }

        public List<QualifiedCorpse> Qualified
        {
            get { return qualified; }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % ScanIntervalTicks != 0)
            {
                return;
            }

            Rescan();
        }

        private void Rescan()
        {
            HcwtSettings settings = HighlightCorpsesWithTechMod.Settings;
            if (settings == null)
            {
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();
            qualified.Clear();
            scratchThings.Clear();

            // Both spawned corpses and those inside graves and containers, in one
            // call - alsoGetSpawnedThings:true means no second source and no risk of
            // double-counting (architecture 10.2). Buried corpses count toward the
            // alert; they simply have nothing to draw an outline on.
            ThingOwnerUtility.GetAllThingsRecursively(
                map,
                ThingRequest.ForGroup(ThingRequestGroup.Corpse),
                scratchThings,
                true,
                null,
                true);

            int examined = 0;
            for (int i = 0; i < scratchThings.Count; i++)
            {
                Corpse corpse = scratchThings[i] as Corpse;
                if (corpse == null)
                {
                    continue;
                }

                examined++;

                string rejectReason;
                bool qualifies = TechCorpseScanner.TryQualify(
                    corpse, settings, scratchFindings, out rejectReason);

                if (qualifies)
                {
                    QualifiedCorpse entry = new QualifiedCorpse { corpse = corpse };
                    entry.findings.AddRange(scratchFindings);
                    qualified.Add(entry);

                    // Fires on change, not every scan - RimWorld caps logging at
                    // ~1000 messages and a battlefield would blow through that in
                    // seconds otherwise (architecture 10.5).
                    if (!previouslyQualifying.Contains(corpse))
                    {
                        for (int f = 0; f < entry.findings.Count; f++)
                        {
                            TechFinding finding = entry.findings[f];
                            HcwtLog.Message("qualify " + corpse.LabelShort + " at " +
                                DescribePlace(corpse) + ": " +
                                finding.hediff.defName + " -> " + finding.yields.defName +
                                " (" + finding.tier + ")");
                        }
                    }
                }
                else if (rejectReason != null && !previouslyQualifying.Contains(corpse))
                {
                    HcwtLog.Message("reject " + corpse.LabelShort + " at " +
                        DescribePlace(corpse) + " - " + rejectReason);
                }
            }

            previouslyQualifying.Clear();
            for (int i = 0; i < qualified.Count; i++)
            {
                previouslyQualifying.Add(qualified[i].corpse);
            }

            watch.Stop();

            // ONLY when the answer changed. This line used to fire on every scan
            // and the 2026-09-06 log carried 792 copies of it - 40% of everything
            // RimWorld wrote that session, from a mod nobody was debugging.
            // RimWorld stops logging entirely at 1000 messages, for the whole
            // game and not just the mod that spent them, so a per-tick line is a
            // defect however cheap the file it lands in. Fourth breach of the
            // standing rule; cut on the order of 2026-09-06.
            if (qualified.Count != lastReportedQualifying || examined != lastReportedExamined)
            {
                lastReportedQualifying = qualified.Count;
                lastReportedExamined = examined;
                HcwtLog.Message("scan " + MapName() + ": " + examined + " corpses, " +
                    qualified.Count + " qualify (" + watch.ElapsedMilliseconds + "ms)");
            }
        }

        public override void MapComponentUpdate()
        {
            HcwtSettings settings = HighlightCorpsesWithTechMod.Settings;
            if (settings == null || !settings.showOutline || qualified.Count == 0)
            {
                return;
            }

            // ONLY the map being looked at. Read off Verse.Game.UpdatePlay in
            // lib\Assembly-CSharp.dll on 2026-09-07: it loops every map and
            // calls Map.MapUpdate on each, and MapUpdate ends by calling
            // MapComponentUtility.MapComponentUpdate unconditionally. So this
            // method runs once per loaded map per frame, while
            // Find.CameraDriver.CurrentViewRect and Graphics.DrawMesh both
            // belong to whichever map is on screen.
            //
            // Without this guard, a qualifying corpse on the OTHER colony was
            // culled against THIS colony's camera and then drawn at its own
            // coordinates on the map you were looking at - a blue outline
            // hanging over empty forest with no corpse, no grave and nothing
            // to click. Reported three times on 2026-09-07 before the cause
            // was found; the give-away was that the phantom moved when the
            // other map's corpses did.
            if (map != Find.CurrentMap)
            {
                return;
            }

            // Cull to what is on screen before drawing. Forty corpses off-screen
            // should cost nothing (architecture 10.3).
            CellRect view = Find.CameraDriver.CurrentViewRect;

            for (int i = 0; i < qualified.Count; i++)
            {
                Corpse corpse = qualified[i].corpse;

                // A corpse in a grave or a container is not spawned and has nothing
                // to draw on. It still counts toward the alert (architecture 10.2).
                if (corpse == null || corpse.Destroyed || !corpse.Spawned)
                {
                    continue;
                }

                if (!view.Contains(corpse.Position))
                {
                    continue;
                }

                DrawOutline(corpse);
            }
        }

        private void DrawOutline(Corpse corpse)
        {
            Mesh mesh;
            Material sourceMaterial;
            bool haveSilhouette = SilhouetteAccess.TryGetSilhouette(corpse, out mesh, out sourceMaterial);

            // Once per corpse, not once per frame - the standing rule. A
            // square instead of a body shape means this fired.
            if (!haveSilhouette && !reportedNoSilhouette.Contains(corpse))
            {
                reportedNoSilhouette.Add(corpse);
                HcwtLog.Message("no silhouette for " + corpse.LabelShort + " at " +
                    DescribePlace(corpse) + " - drawing the halo instead");
            }

            Material material;
            float scale;

            if (haveSilhouette)
            {
                Texture2D texture = sourceMaterial.mainTexture as Texture2D;
                if (texture == null)
                {
                    return;
                }

                // One material per texture, colour carried in the property block
                // below. Varying colour through MaterialPool would allocate a fresh
                // material every frame and leak without bound (architecture 10.8).
                material = MaterialPool.MatFrom(texture, ShaderDatabase.SolidColorBehind, Color.white);
                scale = OutlineScale;
            }
            else
            {
                material = HaloMaterial();
                mesh = MeshPool.plane10;
                scale = HaloScale;
            }

            if (mesh == null || material == null)
            {
                return;
            }

            PropertyBlock.SetColor(ColorPropertyId, PulsedColor());

            Vector3 position = corpse.DrawPos;
            position.y -= Altitudes.AltInc;

            Matrix4x4 matrix = Matrix4x4.TRS(
                position,
                Quaternion.identity,
                new Vector3(scale, 1f, scale));

            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        // "(78, 0, 120) on Instacolony", or the container when it is not on
        // the ground. Position alone was not enough on 2026-09-07: a phantom
        // outline could not be told from a real corpse without knowing which
        // map the corpse belonged to.
        private string DescribePlace(Corpse corpse)
        {
            if (corpse == null)
            {
                return "nowhere";
            }

            if (!corpse.Spawned)
            {
                return "not spawned (in a grave, container or inventory) on " + MapName();
            }

            return corpse.Position + " on " + MapName();
        }

        private string MapName()
        {
            if (map == null)
            {
                return "no map";
            }

            return map.Parent == null ? "map " + map.Index : map.Parent.LabelCap.ToString();
        }

        private Material HaloMaterial()
        {
            if (haloMaterial == null)
            {
                haloMaterial = MaterialPool.MatFrom(
                    BaseContent.WhiteTex, ShaderDatabase.SolidColorBehind, Color.white);
            }

            return haloMaterial;
        }

        // One colour for every qualifying corpse, pulsing once a second between
        // PulseAlphaFloor and full (architecture 10.3/10.4, amended 2026-08-30). The
        // per-tier brightness bands are gone: they were the only reader of
        // QualifiedCorpse.bestTier, so that field went with them.
        //
        // Clamp01 on the pulse is deliberate: PulseBrightness's amplitude semantics
        // were not verified by reflection, only its signature.
        private static Color PulsedColor()
        {
            float pulse = Mathf.Clamp01(Pulser.PulseBrightness(PulseFrequency, PulseAmplitude));
            float alpha = Mathf.Lerp(PulseAlphaFloor, PulseAlphaCeiling, pulse);

            return new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, alpha);
        }

        // The same colour and the same pulse, for a NAME in a dialog rather than a
        // silhouette on the map - UI/TransferableLabelPatch. Sharing this rather
        // than copying the constants is the whole point: the two have to read as
        // one feature, and a second copy of 0.25/0.6/1.0 would drift.
        //
        // The pulse is carried in BRIGHTNESS here, not alpha. Text drawn at 0.65
        // alpha over a row highlight goes muddy rather than dim, and the label
        // still has to be readable at the bottom of the pulse.
        public static Color PulsedLabelColor()
        {
            float pulse = Mathf.Clamp01(Pulser.PulseBrightness(PulseFrequency, PulseAmplitude));
            float scale = Mathf.Lerp(PulseAlphaFloor, PulseAlphaCeiling, pulse);

            return new Color(
                Mathf.Clamp01(OutlineColor.r * scale),
                Mathf.Clamp01(OutlineColor.g * scale),
                Mathf.Clamp01(OutlineColor.b * scale),
                1f);
        }
    }
}
