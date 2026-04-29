using Unity.Cinemachine;
using UnityEngine;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Returns the single rendering <see cref="Camera"/> that the CinemachineBrain owns. Both
    /// FlyCam and TopDown live on this camera — Cinemachine just picks the live vcam by
    /// priority and applies its lens (perspective vs orthographic) to this camera.
    /// </summary>
    public sealed class ActiveCameraProvider : IActiveCameraProvider
    {
        private Camera _brainCamera;

        public Camera Active => ResolveBrainCamera();
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
