using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UniRx;
using UnityEngine;
using Zenject;
public class NetworkServer : MonoBehaviour
{
    [SerializeField] private int _port = 8080;
    [SerializeField] private float _updateInterval = 0.1f;

    private TcpListener _listener;
    private readonly List<TcpClient> _clients = new();
    private bool _isRunning;
    private int _currentTick;

    [Inject] TreasureGenerator _treasureGen;
    [Inject] GolemGenerator _golemsGen;
    [Inject] GameFlowManager _gameFlow;
    [Inject] LobbyManager _lobby;
    [Inject(Id = "Blue")] TeamBase _teamBaseBlue;
    [Inject(Id = "Red")] TeamBase _teamBaseRed;

    private AgentBehaviour[] _allAgents;
    private EnemyAI[] _allGolems;
    private List<WorldItem> _treasuresCache = new();
    private void Start()
    {
        _allAgents = _teamBaseRed.Objects.Concat(_teamBaseBlue.Objects).ToArray();
        _allGolems = _golemsGen.Objects
            .Where(o => o != null)
            .Select(o => o.GetComponent<EnemyAI>())
            .Where(g => g != null)
            .ToArray();

        foreach (var obj in _treasureGen.Objects)
            AddTreasure(obj);

        ObservableExtensions.Subscribe(
            _treasureGen.Objects.ObserveAdd(),
            x => AddTreasure(x.Value)
        ).AddTo(this);

        ObservableExtensions.Subscribe(
           _treasureGen.Objects.ObserveRemove(),
           x => RemoveTreasure(x.Value)
       ).AddTo(this);

        StartServer(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void AddTreasure(GameObject obj)
    {
        if (obj == null) return;

        var item = obj.GetComponent<WorldItem>();
        if (item != null)
            _treasuresCache.Add(item);
    }

    private void RemoveTreasure(GameObject obj)
    {
        if (obj == null) return;

        var item = obj.GetComponent<WorldItem>();
        if (item != null)
            _treasuresCache.Remove(item);
    }

    private int? GetHolder(WorldItem item)
    {
        if (string.IsNullOrEmpty(item.HolderAgentId)) return null;

        return int.TryParse(item.HolderAgentId, out var id) ? id : null;
    }

    private void HandleJoin(ClientCommand cmd)
    {
        _lobby.SetTeam(cmd.id, cmd.team);
    }

    private void HandleReady(ClientCommand cmd)
    {
        _lobby.SetReady(cmd.id);
    }

    private async UniTaskVoid StartServer(CancellationToken token)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            Debug.Log($"<color=green>[Server]</color> Started on port {_port}");
            _gameFlow.StartLobby();
            UpdateLoop(token).Forget();

            while (!token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync().AsUniTask().AttachExternalCancellation(token);
                _clients.Add(client);
                HandleClient(client, token).Forget();
                Debug.Log("<color=blue>[Server]</color> New client connected");
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception e) { Debug.LogError($"Server error: {e.Message}"); }
        finally { StopServer(); }
    }

    private async UniTaskVoid HandleClient(TcpClient client, CancellationToken token)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        try
        {
            while (client.Connected && !token.IsCancellationRequested)
            {
                string json = await reader.ReadLineAsync().AsUniTask().AttachExternalCancellation(token); ;
                if (string.IsNullOrEmpty(json)) break;

                await UniTask.SwitchToMainThread();
                ProcessCommands(json);
            }
        }
        catch (Exception) { }
        finally
        {
            _clients.Remove(client);
            client.Close();
        }
    }

    private void ProcessCommands(string json)
    {
        try
        {
            var batch = JsonUtility.FromJson<ClientCommandBatch>(json);
            if (batch?.actions == null) return;

            foreach (var cmd in batch.actions)
            {
                switch (cmd.action)
                {
                    case "join": HandleJoin(cmd); break;
                    case "ready": HandleReady(cmd); break;
                }

                if (_gameFlow.State != GameState.InGame)
                    continue;

                var agents = _allAgents.Where(a => a.TeamId == cmd.team);

                foreach (var agent in agents)
                {
                    switch (cmd.action)
                    {
                        case "position":
                            agent.SafeSetDestination(cmd.target.GetVector());
                            break;

                        case "pickup":
                            agent.PickUpItem();
                            break;

                        case "drop":
                            agent.DropItem();
                            break;

                        case "steal":
                            agent.StealItem();
                            break;
                    }
                }
            }
        }
        catch { }
    }

    private async UniTaskVoid UpdateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SendWorldState();
            await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval), cancellationToken: token);
            _currentTick++;
        }
    }

    private void SendWorldState()
    {
        if (_clients.Count == 0) return;

        var state = new WorldStateDto
        {
            tick = _currentTick,
            gameTime = Time.time,
            gameState = _gameFlow.State.ToString()
        };

        state.bases.Add(new BaseDto
        {
            team = "blue",
            pos = new Vector3Dto(_teamBaseBlue.transform.position),
            points = _teamBaseBlue.Points
        });

        state.bases.Add(new BaseDto
        {
            team = "red",
            pos = new Vector3Dto(_teamBaseRed.transform.position),
            points = _teamBaseRed.Points
        });

        foreach (var a in _allAgents)
        {
            var nav = a.Agent;

            state.agents.Add(new AgentDto
            {
                agentId = int.TryParse(a.AgentId, out var id) ? id : 0,
                team = a.TeamId,
                pos = new Vector3Dto(a.transform.position),
                weight = a.Weight,
                currentSpeed = a.CurrentSpeed,
                isStunned = a.IsStunned.Value,
                hasTreasure = a.HasTreasure,
                heldTreasureId = a.HeldTreasureId,
                stealAbilityReady = a.StealAbilityReady,
                stealChargePercentage = a.StealChargePercentage,
                destination = a.Destination != null
                    ? new Vector3Dto(a.Destination.Value)
                    : null
            });
        }

        foreach (var g in _allGolems)
        {
            state.golems.Add(new GolemDto
            {
                id = g.name,
                pos = new Vector3Dto(g.transform.position),
                currentSpeed = g.CurrentSpeed,
                state = g.CurrentState,
                targetAgentId = g.TargetAgentId
            });
        }

        foreach (var i in _treasuresCache)
        {
            if (i == null) continue;

            state.treasures.Add(new TreasureDto
            {
                id = i.gameObject.GetEntityId().ToString(),
                pos = new Vector3Dto(i.transform.position),
                isPicked = i.IsPicked,
                holderAgentId = GetHolder(i),
                value = (int)((i.ItemData as TreasureData)?.Cost ?? 0),
                weight = i.ItemData.Weight
            });
        }

        var json =
        JsonConvert.SerializeObject(
        state,
        Formatting.None,
        new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include
        });

        json += "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);

        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            try
            {
                if (_clients[i].Connected)
                    _clients[i].GetStream().Write(data, 0, data.Length);
            }
            catch
            {
                _clients.RemoveAt(i);
            }
        }
    }
    private void StopServer()
    {
        _isRunning = false;
        _listener?.Stop();
        foreach (var client in _clients) client?.Close();
        _clients.Clear();
    }
    private void OnDestroy() => StopServer();
}