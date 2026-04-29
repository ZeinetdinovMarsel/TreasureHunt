using Unity.Cinemachine;
using UnityEngine;

namespace TreasureHunt.Cameras
{
    public sealed class ActiveCameraProvider : IActiveCameraProvider
    {
        private readonly ICameraModeService _modeService;
        private readonly TopDownCameraController _topDown;
        private Camera _brainCamera;

        public ActiveCameraProvider(ICameraModeService modeService, TopDownCameraController topDown)
        {
            _modeService = modeService;
            _topDown = topDown;
        }

        public Camera Active
        {
            get
            {
                if (_modeService.Mode.Value == CameraMode.TopDown && _topDown != null)
                    return _topDown.Camera;

                return ResolveBrainCamera();
            }
        }

        public Camera BrainCamera => ResolveBrainCamera();

        private Camera ResolveBrainCamera()
        {
            if (_brainCamera != null) return _brainCamera;

            var brain = Object.FindFirstObjectByType<CinemachineBrain>();
            if (brain != null) _brainCamera = brain.GetComponent<Camera>();
            if (_brainCamera == null) _brainCamera = Camera.main;
            return _brainCamera;
        }
    }
}
