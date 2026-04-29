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

        public void Toggle()
        {
            _mode.Value = _mode.Value == CameraMode.FlyCam
                ? CameraMode.TopDown
                : CameraMode.FlyCam;
        }
    }
}
