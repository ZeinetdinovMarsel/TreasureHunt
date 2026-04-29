using PrimeTween;
using UniRx;
using UniRx.Triggers;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Heroes 3 style orthographic top-down camera built on Cinemachine. WASD/arrows + screen-edge
    /// panning to move, mouse wheel to zoom (PrimeTween-eased). The component owns its own
    /// <see cref="CinemachineCamera"/>; switching is done by changing priorities so the existing
    /// CinemachineBrain handles blending and lens mode (orthographic vs perspective) automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TopDownCameraController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 30f;
        [SerializeField] private float _runMultiplier = 2f;
        [SerializeField] private float _edgePanThicknessPx = 16f;
        [SerializeField] private bool _edgePanEnabled = true;

        [Header("Zoom")]
        [SerializeField] private float _zoomMin = 8f;
        [SerializeField] private float _zoomMax = 90f;
        [SerializeField] private float _zoomDefault = 35f;
        [SerializeField] private float _zoomStep = 6f;
        [SerializeField] private float _zoomTweenDuration = 0.18f;

        [Header("Placement")]
        [SerializeField] private float _height = 120f;
        [SerializeField] private Vector2 _bounds = new Vector2(500f, 500f);

        [Header("Cinemachine")]
        [SerializeField] private int _activePriority = 1500;
        [SerializeField] private int _inactivePriority = 0;

        public CinemachineCamera Vcam { get; private set; }
        public bool IsActive { get; private set; }

        private Tween _zoomTween;
        private float _targetOrthoSize;

        private void Awake()
        {
            _targetOrthoSize = Mathf.Clamp(_zoomDefault, _zoomMin, _zoomMax);

            Vcam = GetComponent<CinemachineCamera>();
            if (Vcam == null) Vcam = gameObject.AddComponent<CinemachineCamera>();

            ConfigureVcam(Vcam);

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            transform.position = new Vector3(0f, _height, 0f);

            this.UpdateAsObservable()
                .Where(_ => isActiveAndEnabled && IsActive)
                .Subscribe(_ => Tick())
                .AddTo(this);
        }

        private void OnDisable()
        {
            _zoomTween.Stop();
        }

        public void SetActive(bool value)
        {
            IsActive = value;
            if (Vcam != null)
                Vcam.Priority = value ? _activePriority : _inactivePriority;
        }

        public void CenterOn(Vector3 worldPosition)
        {
            transform.position = new Vector3(worldPosition.x, _height, worldPosition.z);
        }

        public void ConfigureBounds(Vector2 bounds) => _bounds = bounds;

        private void ConfigureVcam(CinemachineCamera vcam)
        {
            // Switch the brain camera to orthographic projection while this vcam is live.
            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = _targetOrthoSize;
            lens.NearClipPlane = 0.3f;
            lens.FarClipPlane = _height * 4f;
            vcam.Lens = lens;

            vcam.Priority = _inactivePriority;
            vcam.Follow = null;
            vcam.LookAt = null;
        }

        private void Tick()
        {
            ApplyMovement(Time.unscaledDeltaTime);
            ApplyZoom();
        }

        private void ApplyMovement(float dt)
        {
            Vector2 axis = ReadMovementAxis();
            float orthoSize = Vcam.Lens.OrthographicSize;
            float speed = _moveSpeed * (IsRunning() ? _runMultiplier : 1f) * Mathf.Max(1f, orthoSize / _zoomDefault);

            Vector3 delta = new Vector3(axis.x, 0f, axis.y) * speed * dt;
            Vector3 next = transform.position + delta;

            next.x = Mathf.Clamp(next.x, -_bounds.x * 0.5f, _bounds.x * 0.5f);
            next.z = Mathf.Clamp(next.z, -_bounds.y * 0.5f, _bounds.y * 0.5f);
            next.y = _height;

            transform.position = next;
        }

        private void ApplyZoom()
        {
            float scroll = ReadScroll();
            if (Mathf.Abs(scroll) <= 0.001f) return;

            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize - scroll * _zoomStep, _zoomMin, _zoomMax);
            _zoomTween.Stop();

            float startSize = Vcam.Lens.OrthographicSize;
            _zoomTween = Tween.Custom(
                startValue: startSize,
                endValue: _targetOrthoSize,
                duration: _zoomTweenDuration,
                onValueChange: SetOrthographicSize,
                ease: Ease.OutCubic);
        }

        private void SetOrthographicSize(float size)
        {
            if (Vcam == null) return;
            var lens = Vcam.Lens;
            lens.OrthographicSize = size;
            Vcam.Lens = lens;
        }

        private Vector2 ReadMovementAxis()
        {
            Vector2 axis = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) axis.y += 1f;
            }

            if (_edgePanEnabled && axis.sqrMagnitude < 0.01f)
                axis += ReadEdgePan();

            if (axis.sqrMagnitude > 1f) axis.Normalize();
            return axis;
        }

        private Vector2 ReadEdgePan()
        {
            var mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;

            Vector2 pos = mouse.position.ReadValue();
            if (pos.x < 0f || pos.y < 0f || pos.x > Screen.width || pos.y > Screen.height) return Vector2.zero;

            Vector2 axis = Vector2.zero;
            if (pos.x <= _edgePanThicknessPx) axis.x -= 1f;
            else if (pos.x >= Screen.width - _edgePanThicknessPx) axis.x += 1f;

            if (pos.y <= _edgePanThicknessPx) axis.y -= 1f;
            else if (pos.y >= Screen.height - _edgePanThicknessPx) axis.y += 1f;

            return axis;
        }

        private bool IsRunning()
        {
            var kb = Keyboard.current;
            return kb != null && kb.leftShiftKey.isPressed;
        }

        private float ReadScroll()
        {
            var mouse = Mouse.current;
            if (mouse == null) return 0f;
            float raw = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(raw) < 0.001f) return 0f;
            // Unity's InputSystem reports the scroll delta either as ±1 per notch (modern
            // builds) or as ±120 (Win32 WHEEL_DELTA on older builds). Normalise both into ~±1
            // so the configured zoom step behaves the same regardless of the platform.
            return Mathf.Abs(raw) >= 60f ? raw / 120f : raw;
        }
    }
}
