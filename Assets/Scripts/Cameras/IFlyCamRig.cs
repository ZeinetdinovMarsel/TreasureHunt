using UnityEngine;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Abstraction over the FlyCam's underlying rig (follow target + pan/tilt axes) so callers
    /// can teleport the free-fly camera without having to know how it is composed in Cinemachine.
    /// </summary>
    public interface IFlyCamRig
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        void TeleportTo(Vector3 position, Quaternion rotation);
    }
}
