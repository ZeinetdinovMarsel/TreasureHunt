using UniRx;
using UniRx.Triggers;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Listens for the toggle key and translates <see cref="ICameraModeService"/> changes into
    /// concrete camera state: enables the top-down camera and lowers the FlyCam priority while
    /// it is active, then restores it when going back.
    /// </summary>
    public sealed class CameraSwitcher : IInitializable, ITickable
    {
        private const int ActivePriority = 1000;
        private const int InactivePriority = 0;

        private readonly ICameraModeService _modeService;
        private readonly TopDownCameraController _topDown;
        private readonly CinemachineCamera _flyCam;
        private readonly IActiveCameraProvider _activeCamera;

        private int _flyCamOriginalPriority;

        public CameraSwitcher(
            ICameraModeService modeService,
            TopDownCameraController topDown,
            IActiveCameraProvider activeCamera,
            [Inject(Id = "UserCam")] CinemachineCamera flyCam)
        {
            _modeService = modeService;
            _topDown = topDown;
            _activeCamera = activeCamera;
            _flyCam = flyCam;
        }

        public void Initialize()
        {
            if (_flyCam != null)
                _flyCamOriginalPriority = _flyCam.Priority.Value;

            _modeService.Mode
                .Subscribe(ApplyMode)
                .AddTo(_topDown);
        }

        public void Tick()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
                _modeService.Toggle();
        }

        private void ApplyMode(CameraMode mode)
        {
            bool topDownActive = mode == CameraMode.TopDown;

            if (_flyCam != null)
                _flyCam.Priority = topDownActive ? InactivePriority : Mathf.Max(ActivePriority, _flyCamOriginalPriority);

            _topDown.SetEnabled(topDownActive);

            var brain = (_activeCamera as ActiveCameraProvider)?.BrainCamera;
            if (brain != null)
                brain.enabled = !topDownActive;

            if (topDownActive && _flyCam != null)
                _topDown.CenterOn(_flyCam.transform.position);
        }
    }
}
