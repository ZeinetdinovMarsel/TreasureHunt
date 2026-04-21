using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    [Inject(Id = "Blue")] TeamBase _teamBaseBlue;
    [Inject(Id = "Red")] TeamBase _teamBaseRed;

    private AgentBehaviour[] _allAgents;

    private void Start()
    {
        _allAgents = _teamBaseRed.Objects.Concat(_teamBaseBlue.Objects).ToArray();
        StartServer(this.GetCancellationTokenOnDestroy()).Forget();
    }
    private async UniTaskVoid StartServer(CancellationToken token)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            Debug.Log($"<color=green>[Server]</color> Started on port {_port}");

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
        catch (Exception) {  }
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
                var agent = Array.Find(_allAgents, a => a.AgentId == cmd.id);
                if (agent == null) continue;

                switch (cmd.action)
                {
                    case "position": agent.SafeSetDestination(cmd.target); break;
                    case "pickup": agent.PickUpItem(); break;
                    case "drop": agent.DropItem(); break;
                    case "steal": agent.StealItem(); break;
                }
            }
        }
        catch {}
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

        var state = new WorldStateDto { tick = _currentTick, gameTime = Time.time };

        state.bases.Add(new BaseDto { team = "blue", pos = _teamBaseBlue.transform.position });
        state.bases.Add(new BaseDto { team = "red", pos = _teamBaseRed.transform.position });

        foreach (var a in _allAgents)
        {
            var nav = a.GetComponent<UnityEngine.AI.NavMeshAgent>();
            state.agents.Add(new AgentDto
            {
                id = a.AgentId,
                team = a.TeamId,
                pos = a.transform.position,
                isStunned = a.IsStunned.Value,
                hasTreasure = a.CartBeh.Weight.Value > 0,
                destination = (nav != null && nav.hasPath) ? nav.destination : a.transform.position
            });
        }

        foreach (var g in _golemsGen.Objects)
            state.golems.Add(new GolemDto { id = g.name, pos = g.transform.position });

        var treasures = _treasureGen.Objects
          .Where(o => o != null)
                .Select(o => o.GetComponent<WorldItem>())
            .Where(w => w != null && !w.IsPicked);
        foreach (var i in treasures)
            state.treasures.Add(new TreasureDto
            {
                id = i.gameObject.GetEntityId().ToString(),
                pos = i.transform.position,
                value = (int)((i.ItemData as TreasureData)?.Cost ?? 0),
                weight = i.ItemData.Weight
            });

        string json = JsonUtility.ToJson(state) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);

        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            try { if (_clients[i].Connected) _clients[i].GetStream().Write(data, 0, data.Length); }
            catch { _clients.RemoveAt(i); }
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