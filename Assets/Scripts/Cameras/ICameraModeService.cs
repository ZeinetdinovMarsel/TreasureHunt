using UniRx;

namespace TreasureHunt.Cameras
{
    public interface ICameraModeService
    {
        IReadOnlyReactiveProperty<CameraMode> Mode { get; }
        void SetMode(CameraMode mode);

        /// <summary>Toggles between FlyCam and TopDown (legacy Tab toggle).</summary>
        void Toggle();

        /// <summary>If current mode equals <paramref name="a"/>, switches to <paramref name="b"/>; otherwise switches to <paramref name="a"/>.</summary>
        void ToggleBetween(CameraMode a, CameraMode b);
    }
}
