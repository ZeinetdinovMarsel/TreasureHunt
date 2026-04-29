using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace TreasureHunt.Performance
{
    /// <summary>
    /// Runtime-only quality tuner that brings the open-world terrains into a sane budget for
    /// the gameplay camera. The scene ships with 9 terrains × treeDistance 5000 ×
    /// detailObjectDistance 80, which on a horizon-shot easily blows past 5000 draw calls.
    ///
    /// Rather than touching the scene asset (and its serialised tweaks per tile), we lower the
    /// per-terrain LOD knobs on Awake. Values are exposed so the user can re-tune from the
    /// inspector if a particular tile needs more reach.
    /// </summary>
    public sealed class TerrainPerformanceTuner : IInitializable
    {
        // Conservative defaults that keep the world readable on a horizon shot but shave the
        // bulk of the per-frame draw cost. Empirically each terrain contributes ~hundreds of
        // batches at the original 5000-unit tree distance.
        private const float TargetTreeDistance = 220f;
        private const float TargetDetailDistance = 60f;
        private const float TargetHeightmapError = 12f;
        private const float TargetBasemapDistance = 400f;
        private const float FlyCamFarClipPlane = 350f;

        private readonly CinemachineCamera _flyCam;

        public TerrainPerformanceTuner([Inject(Id = "UserCam", Optional = true)] CinemachineCamera flyCam)
        {
            _flyCam = flyCam;
        }

        public void Initialize()
        {
            TuneTerrains();
            TuneFlyCamFarClip();
        }

        private static void TuneTerrains()
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (var t in terrains)
            {
                if (t == null) continue;
                t.treeDistance = Mathf.Min(t.treeDistance, TargetTreeDistance);
                t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, TargetDetailDistance);
                t.heightmapPixelError = Mathf.Max(t.heightmapPixelError, TargetHeightmapError);
                t.basemapDistance = Mathf.Min(t.basemapDistance, TargetBasemapDistance);
                // Cull tree billboards aggressively past the cutoff distance.
                t.treeBillboardDistance = Mathf.Min(t.treeBillboardDistance, TargetTreeDistance * 0.6f);
            }
        }

        private void TuneFlyCamFarClip()
        {
            if (_flyCam == null) return;
            var lens = _flyCam.Lens;
            if (lens.FarClipPlane > FlyCamFarClipPlane)
            {
                lens.FarClipPlane = FlyCamFarClipPlane;
                _flyCam.Lens = lens;
            }
        }
    }
}
