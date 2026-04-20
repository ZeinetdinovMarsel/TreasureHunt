using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

    [Inject(Id = "Blue")] TeamBase _teamBaseBlue;
    [Inject(Id = "Red")] TeamBase _teamBaseRed;
    private AgentBehaviour[] _allAgents;
    [Inject]
    private void Construct()
    {
        _allAgents = _teamBaseRed.Objects
            .Concat(_teamBaseBlue.Objects)
            .ToArray();
    }
    private void Start()
    {

        StartServer().Forget() ;

    }

    private async UniTaskVoid StartServer()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _isRunning = true;
        Debug.Log($"[Server] Started on port {_port}");
        UpdateLoop().Forget();
        while (_isRunning)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _clients.Add(client);
            HandleClient(client).Forget();
            Debug.Log("[Server] New client connected");
        }
    }

    private async UniTaskVoid HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        byte[] buffer = new byte[4096];

        try
        {
            while (client.Connected)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ProcessCommands(json);
                }
                await UniTask.Yield();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Server] Client error: {e.Message}");
        }
        finally
        {
            _clients.Remove(client);
        }
    }

    private void ProcessCommands(string json)
    {
        try
        {
            var batch = JsonUtility.FromJson<ClientCommandBatch>(json);
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
        catch { }
    }

    private async UniTaskVoid UpdateLoop()
    {
        while (_isRunning)
        {
            SendWorldState();
            await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval));
            _currentTick++;
        }
    }

    private void SendWorldState()
    {
        WorldStateDto state = new WorldStateDto
        {
            tick = _currentTick,
            gameTime = Time.time,
        };


        foreach (var a in _allAgents)
        {
            state.agents.Add(new AgentDto
            {
                id = a.AgentId,
                team = a.TeamId,
                pos = a.transform.position,
                isStunned = a.IsStunned.Value,
                hasTreasure = a.CartBeh.Weight.Value > 0,
                destination = a.GetComponent<UnityEngine.AI.NavMeshAgent>().hasPath
                              ? a.GetComponent<UnityEngine.AI.NavMeshAgent>().destination
                              : (Vector3?)null
            });
        }

        foreach (var g in _golemsGen.Objects)
        {
            state.golems.Add(new GolemDto { id = g.name, pos = g.transform.position });
        }


        var treasures = _treasureGen.Objects
            .ToArray()
            .Select(o => o.GetComponent<WorldItem>())
            .Where(w => w != null);
        foreach (var i in treasures)
        {
            if (i.IsPicked) continue;
            state.treasures.Add(new TreasureDto
            {
                id = i.gameObject.GetEntityId().ToString(),
                pos = i.transform.position,
                value = (int)((i.ItemData as TreasureData)?.Cost ?? 0),
                weight = i.ItemData.Weight
            });
        }

        string json = JsonUtility.ToJson(state);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");

        foreach (var client in _clients.ToArray())
        {
            if (client.Connected)
            {
                client.GetStream().Write(data, 0, data.Length);
            }
        }
    }

    private void OnDestroy()
    {
        _isRunning = false;
        _listener?.Stop();
        foreach (var c in _clients) c.Close();
    }
}