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
    [SerializeField] private float _money;
    [Inject] DiContainer _diContainer;


    protected readonly ReactiveCollection<AgentBehaviour> _objects = new ReactiveCollection<AgentBehaviour>();
    public IReadOnlyReactiveCollection<AgentBehaviour> Objects => _objects;

    private void Awake()
    {
        foreach (var spawnPoint in _spawnPoints)
        {
            var agent = _diContainer.InstantiatePrefab(_agentPrefab, spawnPoint.position, Quaternion.identity, null).GetComponentInChildren<AgentBehaviour>();

            agent.Initialize(agent.GetEntityId().ToString(), _teamType.ToString());

            _objects.Add(agent);

            if (Physics.Raycast(spawnPoint.position, Vector3.down, out var hit, Mathf.Infinity))
            {
                agent.transform.position = hit.point;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Treasure"))
        {
            if(other.TryGetComponent<WorldItem>(out var worldItem) && !worldItem.IsPicked)
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
