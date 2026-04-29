using TreasureHunt.Cameras;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace TreasureHunt.Minimap
{
    /// <summary>
    /// World-space sprite that hovers over a tracked target and is sized in screen pixels rather
    /// than world units, so it stays readable regardless of camera zoom.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MinimapIcon : MonoBehaviour
    {
        private const float MinScreenPx = 18f;
        private const float MaxScreenPx = 42f;
        private const float DefaultScreenPx = 28f;
        private const float ReferenceScreenHeight = 1080f;

        private SpriteRenderer _renderer;
        private Transform _target;
        private float _heightOffset;
        private float _screenPx = DefaultScreenPx;
        private bool _visibleInFlyCam;
        private ICameraModeService _modeService;
        private IActiveCameraProvider _cameraProvider;

        public void Configure(
            Transform target,
            Sprite sprite,
            ICameraModeService modeService,
            IActiveCameraProvider cameraProvider,
            float heightOffset = 3f,
            bool visibleInFlyCam = false,
            float screenPx = DefaultScreenPx,
            int sortingOrder = 1000)
        {
            _target = target;
            _heightOffset = heightOffset;
            _visibleInFlyCam = visibleInFlyCam;
            _screenPx = Mathf.Clamp(screenPx, MinScreenPx, MaxScreenPx);
            _modeService = modeService;
            _cameraProvider = cameraProvider;

            EnsureRenderer();
            _renderer.sprite = sprite;
            _renderer.sortingOrder = sortingOrder;

            if (modeService != null)
            {
                modeService.Mode
                    .Subscribe(UpdateVisibility)
                    .AddTo(this);
            }

            this.LateUpdateAsObservable()
                .Subscribe(_ => Tick())
                .AddTo(this);
        }

        private void EnsureRenderer()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
        }

        private void Tick()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            // Skip per-frame work entirely when the icon is hidden — billboard maths and Vector3
            // distance calls add up quickly when the scene has 30+ icons that are only visible
            // in TopDown mode.
            EnsureRenderer();
            if (_renderer != null && !_renderer.enabled) return;

            transform.position = _target.position + Vector3.up * _heightOffset;

            Camera cam = _cameraProvider != null ? _cameraProvider.Active : Camera.main;
            if (cam == null) return;

            transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

            float scale = ComputeScreenStableScale(cam);
            transform.localScale = new Vector3(scale, scale, scale);
        }

        private float ComputeScreenStableScale(Camera cam)
        {
            // World units that correspond to one pixel on screen — formulas differ for ortho/persp.
            float worldPerPixel;

            if (cam.orthographic)
            {
                float screenH = Mathf.Max(1f, Screen.height);
                worldPerPixel = (cam.orthographicSize * 2f) / screenH;
            }
            else
            {
                float distance = Vector3.Distance(cam.transform.position, transform.position);
                float frustumHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float screenH = Mathf.Max(1f, Screen.height);
                worldPerPixel = frustumHeight / screenH;
            }

            // Slightly grow on small screens so icons don't disappear on low-res displays.
            float screenScale = Mathf.Clamp(Screen.height / ReferenceScreenHeight, 0.6f, 1.6f);

            return _screenPx * worldPerPixel / screenScale;
        }

        private void UpdateVisibility(CameraMode mode)
        {
            EnsureRenderer();
            _renderer.enabled = mode == CameraMode.TopDown || _visibleInFlyCam;
        }

    }
}
