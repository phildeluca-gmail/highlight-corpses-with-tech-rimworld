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
        public TechLevel bestTier;
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

        // A two-second cycle is a frequency of 0.5.
        private const float PulseFrequency = 0.5f;
        private const float PulseAmplitude = 0.6f;

        // How far the silhouette is enlarged to show as a band around the corpse.
        private const float OutlineScale = 1.18f;
        private const float HaloScale = 1.75f;

        // Light blue (architecture 10.3). Brightness varies by tier (10.4); the hue
        // does not.
        private static readonly Color OutlineHue = new Color(0.45f, 0.75f, 1f);

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private readonly List<QualifiedCorpse> qualified = new List<QualifiedCorpse>();
        private readonly List<Thing> scratchThings = new List<Thing>();
        private readonly List<TechFinding> scratchFindings = new List<TechFinding>();
        private readonly HashSet<Corpse> previouslyQualifying = new HashSet<Corpse>();

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
                    entry.bestTier = TechCorpseScanner.HighestTier(entry.findings);
                    qualified.Add(entry);

                    // Fires on change, not every scan - RimWorld caps logging at
                    // ~1000 messages and a battlefield would blow through that in
                    // seconds otherwise (architecture 10.5).
                    if (!previouslyQualifying.Contains(corpse))
                    {
                        for (int f = 0; f < entry.findings.Count; f++)
                        {
                            TechFinding finding = entry.findings[f];
                            HcwtLog.Message("qualify " + corpse.LabelShort + ": " +
                                finding.hediff.defName + " -> " + finding.yields.defName +
                                " (" + finding.tier + ")");
                        }
                    }
                }
                else if (rejectReason != null && !previouslyQualifying.Contains(corpse))
                {
                    HcwtLog.Message("reject " + corpse.LabelShort + " - " + rejectReason);
                }
            }

            previouslyQualifying.Clear();
            for (int i = 0; i < qualified.Count; i++)
            {
                previouslyQualifying.Add(qualified[i].corpse);
            }

            watch.Stop();
            HcwtLog.Message("scan: " + examined + " corpses, " + qualified.Count +
                " qualify (" + watch.ElapsedMilliseconds + "ms)");
        }

        public override void MapComponentUpdate()
        {
            HcwtSettings settings = HighlightCorpsesWithTechMod.Settings;
            if (settings == null || !settings.showOutline || qualified.Count == 0)
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

                DrawOutline(corpse, qualified[i].bestTier);
            }
        }

        private void DrawOutline(Corpse corpse, TechLevel tier)
        {
            Mesh mesh;
            Material sourceMaterial;
            bool haveSilhouette = SilhouetteAccess.TryGetSilhouette(corpse, out mesh, out sourceMaterial);

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

            PropertyBlock.SetColor(ColorPropertyId, ColorFor(tier));

            Vector3 position = corpse.DrawPos;
            position.y -= Altitudes.AltInc;

            Matrix4x4 matrix = Matrix4x4.TRS(
                position,
                Quaternion.identity,
                new Vector3(scale, 1f, scale));

            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
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

        // Architecture 10.4: the hue is always light blue; the TIER drives brightness.
        //
        // The pulse and the tier are multiplying the same channel, so each tier gets a
        // BAND rather than a single value - a dim Industrial corpse at its brightest
        // must never outshine an Archotech corpse at its dimmest, or the tier signal
        // becomes noise. Clamp01 on the pulse is deliberate: PulseBrightness's
        // amplitude semantics were not verified by reflection, only its signature.
        private static Color ColorFor(TechLevel tier)
        {
            float bandFloor = BandFloorFor(tier);
            float bandCeiling = bandFloor + 0.18f;

            float pulse = Mathf.Clamp01(Pulser.PulseBrightness(PulseFrequency, PulseAmplitude));
            float alpha = Mathf.Lerp(bandFloor, bandCeiling, pulse);

            return new Color(OutlineHue.r, OutlineHue.g, OutlineHue.b, alpha);
        }

        private static float BandFloorFor(TechLevel tier)
        {
            switch (tier)
            {
                case TechLevel.Archotech:
                    return 0.76f;
                case TechLevel.Ultra:
                    return 0.58f;
                case TechLevel.Spacer:
                    return 0.40f;
                case TechLevel.Industrial:
                    return 0.22f;
                default:
                    // Neolithic, Medieval and Undefined - only visible at all if the
                    // player turned those tiers on, so they get the dimmest band.
                    return 0.12f;
            }
        }
    }
}
