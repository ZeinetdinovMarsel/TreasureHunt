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
    ///
    /// When leaving Observer for FlyCam we copy the brain camera's current pose onto the FlyCam
    /// vcam so the user reappears exactly where they were watching the agent (Counter-Strike
    /// observer-detach feel) instead of teleporting back to the spot they last free-flew from.
    /// </summary>
    public sealed class CameraSwitcher : IInitializable, ITickable
    {
        private const int FlyCamActivePriority = 1000;

        private readonly ICameraModeService _modeService;
        private readonly IAgentObserverService _observerService;
        private readonly IActiveCameraProvider _activeCameraProvider;
        private readonly IFlyCamRig _flyCamRig;
        private readonly TopDownCameraController _topDown;
        private readonly CinemachineCamera _flyCam;

        private int _flyCamOriginalPriority;
        private CameraMode _previousMode = CameraMode.FlyCam;

        public CameraSwitcher(
            ICameraModeService modeService,
            IAgentObserverService observerService,
            IActiveCameraProvider activeCameraProvider,
            IFlyCamRig flyCamRig,
            TopDownCameraController topDown,
            [Inject(Id = "UserCam")] CinemachineCamera flyCam)
        {
            _modeService = modeService;
            _observerService = observerService;
            _activeCameraProvider = activeCameraProvider;
            _flyCamRig = flyCamRig;
            _topDown = topDown;
            _flyCam = flyCam;
        }

        public void Initialize()
        {
            if (_flyCam != null)
                _flyCamOriginalPriority = Mathf.Max(_flyCam.Priority.Value, FlyCamActivePriority);

            _previousMode = _modeService.Mode.Value;

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
            // Observer → FlyCam: rebase FlyCam to wherever the observer view was rendering so the
            // user does not snap back to their old free-fly spot. The FlyCam follows a separate
            // rigidbody via HardLockToTarget, so we delegate to IFlyCamRig which knows to move
            // both the followed transform and the pan/tilt axes.
            if (_previousMode == CameraMode.Observer && mode == CameraMode.FlyCam && _flyCamRig != null)
            {
                var brain = _activeCameraProvider != null ? _activeCameraProvider.Active : null;
                if (brain != null)
                    _flyCamRig.TeleportTo(brain.transform.position, brain.transform.rotation);
            }

            bool topDownActive = mode == CameraMode.TopDown;
            bool observerActive = mode == CameraMode.Observer;
            bool flyCamActive = mode == CameraMode.FlyCam;

            if (topDownActive)
            {
                Vector3 center = _flyCamRig != null ? _flyCamRig.Position
                    : (_flyCam != null ? _flyCam.transform.position : Vector3.zero);
                _topDown.CenterOn(center);
            }

            _topDown.SetActive(topDownActive);
            _observerService.SetActive(observerActive);

            if (_flyCam != null)
                _flyCam.Priority = flyCamActive ? _flyCamOriginalPriority : 0;

            _previousMode = mode;
        }
    }
}
