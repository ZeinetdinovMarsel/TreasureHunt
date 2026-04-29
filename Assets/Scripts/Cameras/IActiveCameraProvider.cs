using UnityEngine;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Returns whichever <see cref="Camera"/> is currently rendering the world. Provided so that
    /// systems that need to project from world to screen (icons, gizmos, raycasts) do not need to
    /// know about the camera switching logic.
    /// </summary>
    public interface IActiveCameraProvider
    {
        Camera Active { get; }
    }
}
