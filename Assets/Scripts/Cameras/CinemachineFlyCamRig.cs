using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Concrete FlyCam rig: follows a tracked transform via <see cref="CinemachineHardLockToTarget"/>
    /// (or directly via the vcam transform when no target is set) and uses
    /// <see cref="CinemachinePanTilt"/> for look angles. Teleporting therefore needs to move both
    /// the followed transform and the pan/tilt axes to keep the camera consistent.
    /// </summary>
    public sealed class CinemachineFlyCamRig : IFlyCamRig, IInitializable
    {
        private readonly CinemachineCamera _vcam;

        private Transform _followTarget;
        private CinemachinePanTilt _panTilt;

        public CinemachineFlyCamRig([Inject(Id = "UserCam")] CinemachineCamera vcam)
        {
            _vcam = vcam;
        }

        public Vector3 Position => _vcam != null ? _vcam.transform.position : Vector3.zero;
        public Quaternion Rotation => _vcam != null ? _vcam.transform.rotation : Quaternion.identity;

        public void Initialize()
        {
            if (_vcam == null) return;

            // Resolve the follow target: explicit Follow first, then TrackingTarget.
            if (_vcam.Follow != null)
                _followTarget = _vcam.Follow;
            else if (_vcam.Target.TrackingTarget != null)
                _followTarget = _vcam.Target.TrackingTarget;

            _panTilt = _vcam.GetComponent<CinemachinePanTilt>();
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (_vcam == null) return;

            // Move the body. HardLockToTarget snaps to _followTarget every frame so writing the
            // vcam transform alone is not enough; the followed transform must also be moved.
            if (_followTarget != null)
                _followTarget.position = position;
            else
                _vcam.transform.position = position;

            if (_panTilt != null)
            {
                Vector3 euler = rotation.eulerAngles;
                float pitch = NormalizeAngle(euler.x);
                float yaw = NormalizeAngle(euler.y);

                // Cinemachine PanTilt convention: TiltAxis positive = looking up. Unity Euler X
                // is positive when looking DOWN, so flip the sign.
                if (_panTilt.PanAxis != null)
                    _panTilt.PanAxis.Value = ClampToRange(yaw, _panTilt.PanAxis);

                if (_panTilt.TiltAxis != null)
                    _panTilt.TiltAxis.Value = ClampToRange(-pitch, _panTilt.TiltAxis);
            }
            else
            {
                _vcam.transform.rotation = rotation;
            }
        }

        private static float NormalizeAngle(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }

        private static float ClampToRange(float value, InputAxis axis)
        {
            if (axis.Wrap)
                return value;
            return Mathf.Clamp(value, axis.Range.x, axis.Range.y);
        }
    }
}
