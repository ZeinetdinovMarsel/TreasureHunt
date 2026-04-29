using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Listens for the toggle key and translates <see cref="ICameraModeService"/> changes into
    /// concrete Cinemachine priority changes. There is only one rendering camera in the scene
    /// (the CinemachineBrain camera); switching modes simply boosts whichever vcam should be
    /// live so the brain blends to it.
    /// </summary>
    public sealed class CameraSwitcher : IInitializable, ITickable
    {
        private const int ActivePriority = 1000;

        private readonly ICameraModeService _modeService;
        private readonly TopDownCameraController _topDown;
        private readonly CinemachineCamera _flyCam;

        private int _flyCamOriginalPriority;

        public CameraSwitcher(
            ICameraModeService modeService,
            TopDownCameraController topDown,
            [Inject(Id = "UserCam")] CinemachineCamera flyCam)
        {
            _modeService = modeService;
            _topDown = topDown;
            _flyCam = flyCam;
        }

        public void Initialize()
        {
            if (_flyCam != null)
                _flyCamOriginalPriority = Mathf.Max(_flyCam.Priority.Value, ActivePriority);

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

            if (topDownActive && _flyCam != null)
                _topDown.CenterOn(_flyCam.transform.position);

            _topDown.SetActive(topDownActive);

            if (_flyCam != null)
                _flyCam.Priority = topDownActive ? 0 : _flyCamOriginalPriority;
        }
    }
}
