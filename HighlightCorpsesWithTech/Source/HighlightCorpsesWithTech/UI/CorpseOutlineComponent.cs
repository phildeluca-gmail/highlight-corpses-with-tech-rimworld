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

                DrawOutline(corpse);
            }
        }

        private void DrawOutline(Corpse corpse)
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

            PropertyBlock.SetColor(ColorPropertyId, PulsedColor());

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
    }
}
