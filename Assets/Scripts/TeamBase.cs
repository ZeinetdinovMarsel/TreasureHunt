using UniRx;
using UnityEngine;
using Zenject;
public enum TeamType
{
    Blue,
    Red
}

public class TeamBase : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private GameObject _agentPrefab;
    [SerializeField] private TeamType _teamType;
    [SerializeField] private float _startMoney = 0;

    [Inject] DiContainer _diContainer;

    [SerializeField] private float _money;
    public float Points => _money;
    public string Team => _teamType.ToString().ToLower();
    public TeamType TeamType => _teamType;



    protected readonly ReactiveCollection<AgentBehaviour> _objects = new ReactiveCollection<AgentBehaviour>();
    public IReadOnlyReactiveCollection<AgentBehaviour> Objects => _objects;

    private void Awake()
    {
        SpawnAgents();
        _money = _startMoney;
    }

    private void SpawnAgents()
    {
        foreach (var spawnPoint in _spawnPoints)
        {
            var agent = _diContainer
                .InstantiatePrefab(_agentPrefab, spawnPoint.position, Quaternion.identity, null)
                .GetComponentInChildren<AgentBehaviour>();

            agent.Initialize(agent.GetEntityId().ToString(), _teamType.ToString().ToLower());

            _objects.Add(agent);

            if (Physics.Raycast(spawnPoint.position, Vector3.down, out var hit, Mathf.Infinity))
                agent.transform.position = hit.point;
        }
    }

    public void ResetTeam()
    {
        _money = _startMoney;

        for (int i = 0; i < _objects.Count; i++)
        {
            var agent = _objects[i];
            if (agent == null) continue;

            agent.ResetState();

            var spawnPoint = _spawnPoints[i % _spawnPoints.Length];

            if (agent.Agent != null && agent.Agent.isOnNavMesh)
                agent.Agent.Warp(spawnPoint.position);
            else
                agent.transform.position = spawnPoint.position;

            agent.transform.rotation = spawnPoint.rotation;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Treasure"))
        {
            if (other.TryGetComponent<WorldItem>(out var worldItem) && !worldItem.IsPicked)
            {
                var item = worldItem.ItemData as TreasureData;
                if (item != null)
                {
                    _money += item.Cost;
                    Destroy(worldItem.MainObject);
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Treasure"))
        {
            if (other.TryGetComponent<WorldItem>(out var worldItem) && !worldItem.IsPicked)
            {
                var item = worldItem.ItemData as TreasureData;
                if (item != null)
                {
                    _money += item.Cost;
                    Destroy(worldItem.MainObject);
                }
            }
        }
    }
}
