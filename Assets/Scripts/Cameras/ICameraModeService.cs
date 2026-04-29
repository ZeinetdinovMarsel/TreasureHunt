using UniRx;

namespace TreasureHunt.Cameras
{
    public interface ICameraModeService
    {
        IReadOnlyReactiveProperty<CameraMode> Mode { get; }
        void SetMode(CameraMode mode);
        void Toggle();
    }
}
