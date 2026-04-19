using UnityEngine;
using Zenject;

public class TeamBase : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private GameObject _agentPrefab;

    [SerializeField] private float _money;
    [Inject] DiContainer _diContainer;
    private void Awake()
    {
        foreach (var spawnPoint in _spawnPoints)
        {
            GameObject agent = _diContainer.InstantiatePrefab(_agentPrefab, spawnPoint.position, Quaternion.identity, null);

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
