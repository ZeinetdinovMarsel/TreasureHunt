using System.Collections.Generic;
using TreasureHunt.Cameras;
using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace TreasureHunt.Performance
{
    /// <summary>
    /// Runtime-only quality tuner that brings the open-world terrains into a sane budget for
    /// the gameplay camera. The scene ships with 9 terrains × treeDistance 5000 ×
    /// detailObjectDistance 80; on a horizon shot that blows past several thousand draw calls
    /// and the CPU spends most of the frame culling and submitting tree/detail patches.
    ///
    /// Strategy:
    ///   • on startup, cap tree/detail/basemap distances and increase heightmapPixelError;
    ///   • subscribe to <see cref="ICameraModeService"/> and clamp tree distance to 0 while
    ///     the player is in TopDown — at 120 m altitude trees become single pixels anyway, so
    ///     paying their CPU+GPU cost is wasted work and was the dominant remaining culprit
    ///     when zooming out the orthographic camera.
    /// </summary>
    public sealed class TerrainPerformanceTuner : IInitializable, System.IDisposable
    {
        // Conservative defaults for the 3D camera that keep the world readable on a horizon
        // shot but shave the bulk of the per-frame draw cost.
        private const float DefaultTreeDistance = 200f;
        private const float DefaultDetailDistance = 50f;
        private const float DefaultBasemapDistance = 350f;
        private const float TargetHeightmapError = 14f;
        private const float FlyCamFarClipPlane = 350f;

        // TopDown overrides: trees/details are not meaningful from 120 m up, so cull them.
        private const float TopDownTreeDistance = 0f;
        private const float TopDownDetailDistance = 0f;

        private readonly CinemachineCamera _flyCam;
        private readonly ICameraModeService _modeService;
        private readonly List<Terrain> _terrains = new List<Terrain>();
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        public TerrainPerformanceTuner(
            ICameraModeService modeService,
            [Inject(Id = "UserCam", Optional = true)] CinemachineCamera flyCam)
        {
            _modeService = modeService;
            _flyCam = flyCam;
        }

        public void Initialize()
        {
            CacheTerrains();
            ApplyHeightmapError();
            TuneFlyCamFarClip();

            _modeService.Mode
                .Subscribe(ApplyModeProfile)
                .AddTo(_disposables);
        }

        public void Dispose() => _disposables.Dispose();

        private void CacheTerrains()
        {
            _terrains.Clear();
            _terrains.AddRange(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None));
        }

        private void ApplyHeightmapError()
        {
            foreach (var t in _terrains)
            {
                if (t == null) continue;
                t.heightmapPixelError = Mathf.Max(t.heightmapPixelError, TargetHeightmapError);
                t.basemapDistance = Mathf.Min(t.basemapDistance, DefaultBasemapDistance);
            }
        }

        private void ApplyModeProfile(CameraMode mode)
        {
            bool topDown = mode == CameraMode.TopDown;
            float treeDistance = topDown ? TopDownTreeDistance : DefaultTreeDistance;
            float detailDistance = topDown ? TopDownDetailDistance : DefaultDetailDistance;

            foreach (var t in _terrains)
            {
                if (t == null) continue;
                t.treeDistance = treeDistance;
                t.detailObjectDistance = detailDistance;
                t.treeBillboardDistance = treeDistance * 0.6f;
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
