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

    [Inject] private TreasureGenerator _treasureGen;
    [Inject] private GolemGenerator _golemsGen;
    [Inject] private GameFlowManager _gameFlow;
    [Inject] private LobbyManager _lobby;
    [Inject(Id = "Blue")] private TeamBase _teamBaseBlue;
    [Inject(Id = "Red")] private TeamBase _teamBaseRed;

    private readonly Dictionary<string, AgentBehaviour> _agentsMap = new();
    private readonly List<WorldItem> _treasuresCache = new();
    private readonly List<EnemyAI> _golemsCache = new();

    private readonly Dictionary<TcpClient, ClientSession> _sessions = new();
    private readonly HashSet<string> _occupiedTeams = new(StringComparer.OrdinalIgnoreCase);
    private int _nextPlayerId = 1;
    private GameState _lastBroadcastState = GameState.Lobby;

    private readonly WorldStateDto _reusableState = new()
    {
        bases = new List<BaseDto>(2),
        agents = new List<AgentDto>(20),
        treasures = new List<TreasureDto>(100),
        golems = new List<GolemDto>(20)
    };

    private void Start()
    {
        var allAgents = _teamBaseRed.Objects.Concat(_teamBaseBlue.Objects);
        foreach (var agent in allAgents)
        {
            if (agent != null && agent.TryGetComponent<AgentBehaviour>(out var behaviour))
                _agentsMap[behaviour.AgentId] = behaviour;
        }

        foreach (var obj in _golemsGen.Objects) AddGolemToCache(obj);
        _golemsGen.Objects.ObserveAdd().Subscribe(x => AddGolemToCache(x.Value)).AddTo(this);
        _golemsGen.Objects.ObserveRemove().Subscribe(x => RemoveGolemFromCache(x.Value)).AddTo(this);

        foreach (var obj in _treasureGen.Objects) AddTreasureToCache(obj);
        _treasureGen.Objects.ObserveAdd().Subscribe(x => AddTreasureToCache(x.Value)).AddTo(this);
        _treasureGen.Objects.ObserveRemove().Subscribe(x => RemoveTreasureFromCache(x.Value)).AddTo(this);

        _gameFlow.StartLobby();
        _lastBroadcastState = _gameFlow.State.Value;
        _gameFlow.State.Subscribe(OnGameStateChanged).AddTo(this);

        StartServer(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid StartServer(CancellationToken token)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            UpdateLoop(token).Forget();

            while (!token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync().AsUniTask().AttachExternalCancellation(token);
                _clients.Add(client);
                HandleClient(client, token).Forget();
            }
        }
        catch (Exception e) when (!(e is OperationCanceledException))
        {
            Debug.LogError($"Server Error: {e.Message}");
        }
        finally
        {
            StopServer();
        }
    }

    private async UniTaskVoid HandleClient(TcpClient client, CancellationToken token)
    {
        NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        ClientSession session = null;

        try
        {
            await UniTask.SwitchToMainThread();
            if (!_clients.Contains(client)) _clients.Add(client);
            Debug.Log("[Сервер] Клиент подключен для прослушивания");

            while (client.Connected && !token.IsCancellationRequested)
            {
                string json = await reader.ReadLineAsync().AsUniTask();
                if (string.IsNullOrWhiteSpace(json)) continue;
                await UniTask.SwitchToMainThread();

                if (session == null && json.Contains("\"team\""))
                {
                    try
                    {
                        var joinReq = JsonConvert.DeserializeObject<JoinRequest>(json);
                        if (joinReq != null && !string.IsNullOrEmpty(joinReq.team))
                        {
                            if (_occupiedTeams.Contains(joinReq.team))
                            {
                                await SendSingleJsonAsync(stream, new { type = "joinRejected", reason = "occupied" }, token);
                                continue;
                            }

                            session = new ClientSession
                            {
                                PlayerId = $"player_{_nextPlayerId++}",
                                Team = joinReq.team,
                                Client = client
                            };

                            _sessions[client] = session;
                            _occupiedTeams.Add(session.Team);
                            _lobby.SetTeam(session.PlayerId, session.Team);

                            await SendSingleJsonAsync(stream, new { type = "joinAccepted", playerId = session.PlayerId, team = session.Team }, token);
                            Debug.Log($"[Сервер] Зарегистрирован {session.PlayerId} для команды {session.Team}");
                            continue;
                        }
                    }
                    catch (Exception ex) { Debug.LogWarning("Ошибка при подключении: " + ex.Message); }
                }

                if (session != null)
                {
                    ProcessCommands(json, session);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[Сервер] Ошибка обработки клиента: {e.Message}");
        }
        finally
        {
            CleanupClient(client);
        }
    }
    private void CleanupClient(TcpClient client)
    {
        if (client == null) return;

        if (_sessions.TryGetValue(client, out var session))
        {
            if (!string.IsNullOrEmpty(session.Team))
            {
                _occupiedTeams.Remove(session.Team);
                Debug.Log($"[Сервер] Команда {session.Team} теперь свободна");
            }
            _sessions.Remove(client);
        }

        lock (_clients)
        {
            _clients.Remove(client);
        }

        try
        {
            if (client.Connected)
            {
                client.GetStream().Close();
            }
            client.Close();
            client.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Сервер] Ошибка при очистке: {e.Message}");
        }

        Debug.Log("[Сервер] Клиент отключился");
    }
    private void ProcessCommands(string json, ClientSession session)
    {
        try
        {
            var batch = JsonUtility.FromJson<ClientCommandBatch>(json);
            if (batch?.actions == null) return;

            foreach (var cmd in batch.actions)
            {
                if (_gameFlow.State.Value != GameState.InGame)
                {
                    if (cmd.action == "ready") _lobby.SetReady(session.PlayerId);
                    continue;
                }

                if (_agentsMap.TryGetValue(cmd.Id, out var agent))
                {
                    if (!string.Equals(agent.TeamId, session.Team, StringComparison.OrdinalIgnoreCase)) continue;

                    switch (cmd.action)
                    {
                        case "position": agent.SafeSetDestination(cmd.target.GetVector()); break;
                        case "pickup": agent.PickUpItem(); break;
                        case "drop": agent.DropItem(); break;
                        case "steal": agent.StealItem(); break;
                    }
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("Parse Error: " + e.Message); }
    }

    private async UniTaskVoid UpdateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_isRunning && _clients.Count > 0)
            {
                SyncWorldState();
                BroadcastJson(_reusableState);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval), cancellationToken: token);
            _currentTick++;
        }
    }

    private void SyncWorldState()
    {
        _reusableState.tick = _currentTick;
        _reusableState.gameTime = Time.time;
        _reusableState.gameState = _gameFlow.State.Value.ToString();

        _reusableState.bases.Clear();
        _reusableState.bases.Add(new BaseDto { team = "blue", pos = new Vector3Dto(_teamBaseBlue.transform.position), points = _teamBaseBlue.Points });
        _reusableState.bases.Add(new BaseDto { team = "red", pos = new Vector3Dto(_teamBaseRed.transform.position), points = _teamBaseRed.Points });

        _reusableState.agents.Clear();
        foreach (var a in _agentsMap.Values)
        {
            _reusableState.agents.Add(new AgentDto
            {
                agentId = a.AgentId,
                team = a.TeamId,
                pos = new Vector3Dto(a.transform.position),
                hasTreasure = a.HasTreasure,
                isStunned = a.IsStunned.Value
            });
        }

        _reusableState.golems.Clear();
        foreach (var g in _golemsCache)
        {
            if (g == null) continue;
            _reusableState.golems.Add(new GolemDto
            {
                id = g.gameObject.GetEntityId().ToString(),
                pos = new Vector3Dto(g.transform.position),
                currentSpeed = g.CurrentSpeed,
                state = g.CurrentState
            });
        }

        _reusableState.treasures.Clear();
        foreach (var i in _treasuresCache)
        {
            if (i == null) continue;
            _reusableState.treasures.Add(new TreasureDto
            {
                id = i.gameObject.GetEntityId().ToString(),
                pos = new Vector3Dto(i.transform.position),
                isPicked = i.IsPicked
            });
        }
    }

    private void BroadcastJson(object obj)
    {
        string json = JsonConvert.SerializeObject(obj) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);

        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            try
            {
                if (_clients[i].Connected)
                    _clients[i].GetStream().WriteAsync(data, 0, data.Length).AsUniTask().Forget();
                else
                    _clients.RemoveAt(i);
            }
            catch
            {
                _clients.RemoveAt(i);
            }
        }
    }

    private async UniTask SendSingleJsonAsync(Stream stream, object obj, CancellationToken token)
    {
        string json = JsonConvert.SerializeObject(obj) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(data, 0, data.Length, token);
    }

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == _lastBroadcastState) return;
        _lastBroadcastState = newState;

        if (newState == GameState.InGame)
            BroadcastJson(new { type = "gameEvent", eventType = "start" });
        else if (newState == GameState.Finished)
        {
            BroadcastJson(new { type = "gameEvent", eventType = "end" });
            float b = _teamBaseBlue.Points, r = _teamBaseRed.Points;
            BroadcastJson(new
            {
                type = "gameEvent",
                eventType = "result",
                winner = Mathf.Approximately(b, r) ? "draw" : (b > r ? "blue" : "red"),
                scores = new Dictionary<string, float> { { "blue", b }, { "red", r } }
            });
        }
    }

    private void AddTreasureToCache(GameObject obj) { if (obj != null && obj.TryGetComponent<WorldItem>(out var item)) _treasuresCache.Add(item); }
    private void RemoveTreasureFromCache(GameObject obj) { if (obj != null && obj.TryGetComponent<WorldItem>(out var item)) _treasuresCache.Remove(item); }

    private void AddGolemToCache(GameObject obj)
    {
        if (obj != null)
        {
            var enemyAI = obj.GetComponentInChildren<EnemyAI>();
            if (enemyAI != null)
                _golemsCache.Add(enemyAI);
        }
    }
    private void RemoveGolemFromCache(GameObject obj)
    {
        if (obj != null)
        {
            var enemyAI = obj.GetComponentInChildren<EnemyAI>();
            if (enemyAI != null)
                _golemsCache.Remove(enemyAI);
        }
    }

    public void ResetGame() { _currentTick = 0; BroadcastJson(new { type = "gameEvent", eventType = "reset" }); SyncWorldState(); }

    private void StopServer()
    {
        _isRunning = false;
        _listener?.Stop();
        foreach (var client in _clients) client?.Close();
        _clients.Clear();
    }

    private void OnDestroy() => StopServer();
}