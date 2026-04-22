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

    public IReadOnlyReactiveProperty<GameState> State => _state;
    private readonly ReactiveProperty<GameState> _state = new ReactiveProperty<GameState>(GameState.Lobby);

    [Inject] private GolemGenerator _golemsGen;
    [Inject] private TreasureGenerator _treasureGen;
    [Inject] private LobbyManager _lobby;
    [Inject] private TeamBase[] _teams;

    private CancellationTokenSource _cts;

    public void StartLobby()
    {
        _state.Value = GameState.Lobby;
        Debug.Log("Лобби. Ожидание игроков");
    }

    public void ResetToLobby()
    {
        _cts?.Cancel();
        _state.Value = GameState.Lobby;
    }

    public void StartMatch()
    {
        if (_state.Value != GameState.Lobby || !_lobby.AllReady.Value)
            return;

        _state.Value = GameState.InGame;
        Debug.Log("Матч начался");

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        RunMatchLoop(_cts.Token).Forget();
    }

    public void ResetMatch()
    {
        if (_state.Value != GameState.Lobby && _state.Value != GameState.Finished || !_lobby.AllReady.Value)
            return;

        foreach(var team in _teams)
        {
            team.ResetTeam();
        }

        _treasureGen.Rebuild();
        _golemsGen.Rebuild();
        ResetToLobby();
    }

    private async UniTaskVoid RunMatchLoop(CancellationToken token)
    {
        float time = 0f;

        while (!token.IsCancellationRequested)
        {
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);

            if (_state.Value != GameState.InGame)
                break;

            time += Time.fixedDeltaTime;

            bool timeOver = time >= _matchDuration;
            bool noTreasures = _treasureGen.Objects.Count == 0;

            if (timeOver || noTreasures)
            {
                EndMatch();
                break;
            }
        }
    }

    public void EndMatch()
    {
        if (_state.Value == GameState.Finished)
            return;

        _state.Value = GameState.Finished;
        Debug.Log("Матч закончился");

        _cts?.Cancel();

        ShowResults().Forget();
    }

    public string GetWinnerName()
    {
        var winner = _teams.OrderByDescending(t => t.Points).FirstOrDefault();
        if (winner == null)
            return "Ничья";

        if (_teams.Length > 1 && _teams.All(t => t.Points == _teams[0].Points))
            return "Ничья";

        return $"Победили {winner.Team}!";
    }

    private async UniTaskVoid ShowResults()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(3), DelayType.UnscaledDeltaTime);
    }
}

public enum GameState
{
    Lobby,
    InGame,
    Finished
}