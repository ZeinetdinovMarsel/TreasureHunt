using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Listens for the toggle keys and translates <see cref="ICameraModeService"/> changes into
    /// concrete Cinemachine priority changes. There is only one rendering camera in the scene
    /// (the CinemachineBrain camera); switching modes simply boosts whichever vcam should be
    /// live so the brain blends to it.
    ///
    /// Bindings:
    ///  • Tab   — toggle FlyCam / TopDown (minimap mode).
    ///  • Space — toggle FlyCam / Observer (CS:GO style: free-fly ↔ watch agents).
    ///  • Q / E or Mouse4 / Mouse5 — prev / next observed agent (Observer mode only).
    /// </summary>
    public sealed class CameraSwitcher : IInitializable, ITickable
    {
        private const int FlyCamActivePriority = 1000;

        private readonly ICameraModeService _modeService;
        private readonly IAgentObserverService _observerService;
        private readonly TopDownCameraController _topDown;
        private readonly CinemachineCamera _flyCam;

        private int _flyCamOriginalPriority;

        public CameraSwitcher(
            ICameraModeService modeService,
            IAgentObserverService observerService,
            TopDownCameraController topDown,
            [Inject(Id = "UserCam")] CinemachineCamera flyCam)
        {
            _modeService = modeService;
            _observerService = observerService;
            _topDown = topDown;
            _flyCam = flyCam;
        }

        public void Initialize()
        {
            if (_flyCam != null)
                _flyCamOriginalPriority = Mathf.Max(_flyCam.Priority.Value, FlyCamActivePriority);

            _modeService.Mode
                .Subscribe(ApplyMode)
                .AddTo(_topDown);
        }

        public void Tick()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame)
                    _modeService.ToggleBetween(CameraMode.FlyCam, CameraMode.TopDown);

                if (kb.spaceKey.wasPressedThisFrame)
                    _modeService.ToggleBetween(CameraMode.FlyCam, CameraMode.Observer);

                if (_modeService.Mode.Value == CameraMode.Observer)
                {
                    if (kb.eKey.wasPressedThisFrame) _observerService.Next();
                    if (kb.qKey.wasPressedThisFrame) _observerService.Previous();
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && _modeService.Mode.Value == CameraMode.Observer)
            {
                if (mouse.forwardButton != null && mouse.forwardButton.wasPressedThisFrame) _observerService.Next();
                if (mouse.backButton != null && mouse.backButton.wasPressedThisFrame) _observerService.Previous();
            }
        }

        private void ApplyMode(CameraMode mode)
        {
            bool topDownActive = mode == CameraMode.TopDown;
            bool observerActive = mode == CameraMode.Observer;
            bool flyCamActive = mode == CameraMode.FlyCam;

            if (topDownActive && _flyCam != null)
                _topDown.CenterOn(_flyCam.transform.position);

            _topDown.SetActive(topDownActive);
            _observerService.SetActive(observerActive);

            if (_flyCam != null)
                _flyCam.Priority = flyCamActive ? _flyCamOriginalPriority : 0;
        }
    }
}
