using UnityEngine;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private GolemGenerator _golemGen;
    [SerializeField] private TreasureGenerator _treasureGen;
    [SerializeField] private TeamBase _blueBase;
    [SerializeField] private TeamBase _redBase;
    [SerializeField] private NetworkServer _networkServer;
    [SerializeField] private GameFlowManager _gameFlowManager;
    [SerializeField] private LobbyManager _lobbyManager;

    public override void InstallBindings()
    {
        Container.Bind<GolemGenerator>().FromInstance(_golemGen).AsSingle();
        Container.Bind<TreasureGenerator>().FromInstance(_treasureGen).AsSingle();
        Container.Bind<TeamBase>().WithId(_blueBase.TeamType.ToString()).FromInstance(_blueBase).AsCached();
        Container.Bind<TeamBase>().WithId(_redBase.TeamType.ToString()).FromInstance(_redBase).AsCached();
        Container.Bind<TeamBase>().FromInstance(_blueBase).AsCached();
        Container.Bind<TeamBase>().FromInstance(_redBase).AsCached();

        Container.Bind<NetworkServer>().FromInstance(_networkServer).AsCached();
        Container.Bind<GameFlowManager>().FromInstance(_gameFlowManager).AsCached();
        Container.Bind<LobbyManager>().FromInstance(_lobbyManager).AsCached();
    }
}
