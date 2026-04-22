using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    private readonly Dictionary<string, string> _playerTeams = new();
    private readonly HashSet<string> _readyPlayers = new();

    private readonly ReactiveProperty<int> _redCount = new(0);
    private readonly ReactiveProperty<int> _blueCount = new(0);
    private readonly ReactiveProperty<int> _readyCount = new(0);

    private readonly ReactiveProperty<bool> _allReady = new(false);

    public IReadOnlyReactiveProperty<int> Red => _redCount;
    public IReadOnlyReactiveProperty<int> Blue => _blueCount;
    public IReadOnlyReactiveProperty<int> Ready => _readyCount;

    public IReadOnlyReactiveProperty<bool> AllReady => _allReady;

    public void SetTeam(string playerId, string team)
    {
        _playerTeams[playerId] = team;

        RecalculateTeams();
        EvaluateReady();
    }

    public void SetReady(string playerId)
    {
        _readyPlayers.Add(playerId);

        EvaluateReady();
    }

    public void SetUnready(string playerId)
    {
        _readyPlayers.Remove(playerId);

        EvaluateReady();
    }

    private void RecalculateTeams()
    {
        int red = _playerTeams.Values.Count(t => t == TeamType.Red.ToString().ToLower());
        int blue = _playerTeams.Values.Count(t => t == TeamType.Blue.ToString().ToLower());

        _redCount.Value = red;
        _blueCount.Value = blue;
    }

    private void EvaluateReady()
    {
        int ready = _readyPlayers.Count;
        _readyCount.Value = ready;

        bool allReady =
            _playerTeams.Count > 0 &&
            _playerTeams.Keys.All(_readyPlayers.Contains);

        _allReady.Value = allReady;
    }

    public void ResetLobby()
    {
        _playerTeams.Clear();
        _readyPlayers.Clear();

        _redCount.Value = 0;
        _blueCount.Value = 0;
        _readyCount.Value = 0;
        _allReady.Value = false;
    }
    public string GetTeam(string playerId)
    {
        return _playerTeams.TryGetValue(playerId, out var team)
            ? team
            : "none";
    }
}