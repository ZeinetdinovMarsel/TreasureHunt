using UniRx;

namespace TreasureHunt.Cameras
{
    public sealed class CameraModeService : ICameraModeService
    {
        private readonly ReactiveProperty<CameraMode> _mode = new ReactiveProperty<CameraMode>(CameraMode.FlyCam);

        public IReadOnlyReactiveProperty<CameraMode> Mode => _mode;

        public void SetMode(CameraMode mode)
        {
            if (_mode.Value == mode) return;
            _mode.Value = mode;
        }

        public void Toggle() => ToggleBetween(CameraMode.FlyCam, CameraMode.TopDown);

        public void ToggleBetween(CameraMode a, CameraMode b)
        {
            _mode.Value = _mode.Value == a ? b : a;
        }
    }
}
