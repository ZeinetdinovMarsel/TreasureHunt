using UnityEngine;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private GolemGenerator _golemGen;
    [SerializeField] private TreasureGenerator _treasureGen;
    [SerializeField] private TeamBase _blueBase;
    [SerializeField] private TeamBase _redBase;

    public override void InstallBindings()
    {
        Container.Bind<GolemGenerator>().FromInstance(_golemGen).AsSingle();
        Container.Bind<TreasureGenerator>().FromInstance(_treasureGen).AsSingle();
        Container.Bind<TeamBase>().WithId("Blue").FromInstance(_blueBase).AsCached();
        Container.Bind<TeamBase>().WithId("Red").FromInstance(_redBase).AsCached();
    }
}
