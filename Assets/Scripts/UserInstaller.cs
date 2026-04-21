using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class UserInstaller : MonoInstaller
{
    [SerializeField] private CinemachineCamera _userCamera;
    [SerializeField] private CameraMovementController _cameraMC;
    [SerializeField] private UserInputManager _userInput;

    public override void InstallBindings()
    {
        Container.Bind<CinemachineCamera>().WithId("UserCam").FromInstance(_userCamera).AsCached();
        Container.Bind<CameraMovementController>().FromInstance(_cameraMC).AsSingle();
        Container.Bind<UserInputManager>().FromInstance(_userInput).AsSingle();
    }
}
