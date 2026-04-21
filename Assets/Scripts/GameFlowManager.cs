using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;
using UniRx;
using UnityEngine;
using Zenject;
using Zenject.Asteroids;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private float _matchDuration = 600f;

    public GameState State { get; private set; }

    [Inject] private TreasureGenerator _treasures;
    [Inject] private LobbyManager _lobby;
    private CancellationTokenSource _cts;

    private void Start()
    {
        Time.timeScale = 0;
        _lobby.AllReady
            .Where(x => x == true)
            .Subscribe(_ =>
            {
                if (State == GameState.Lobby)
                    StartMatch();
            })
            .AddTo(this);
    }

    public void EnterMenu()
    {
        State = GameState.Menu;
        Debug.Log("MENU");
    }

    public void StartLobby()
    {
        State = GameState.Lobby;
        Debug.Log("LOBBY: waiting teams...");
    }

    public void StartMatch()
    {
        if (State != GameState.Lobby) return;

        State = GameState.InGame;
        Debug.Log("MATCH STARTED");

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        Time.timeScale = 1;
        RunMatchLoop(_cts.Token).Forget();
    }

    private async UniTaskVoid RunMatchLoop(CancellationToken token)
    {
        float time = 0f;

        while (!token.IsCancellationRequested)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);

            if (State != GameState.InGame)
                break;

            time += Time.deltaTime;

            bool timeOver = time >= _matchDuration;
            bool noTreasures = _treasures.Objects.Count == 0;

            if (timeOver || noTreasures)
            {
                EndMatch();
                break;
            }
        }
    }

    public void EndMatch()
    {
        if (State == GameState.Finished) return;

        State = GameState.Finished;
        Debug.Log("MATCH FINISHED");

        _cts?.Cancel();
        Time.timeScale = 0;
        ShowResults().Forget();
    }

    private async UniTaskVoid ShowResults()
    {
        await UniTask.Delay(3000);

        ReturnToMenu();
    }

    public void ReturnToMenu()
    {
        State = GameState.Menu;
        Debug.Log("BACK TO MENU");
    }
}

public enum GameState
{
    Menu,
    Lobby,
    InGame,
    Finished
}