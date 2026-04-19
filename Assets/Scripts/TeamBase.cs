using UnityEngine;
using Zenject;

public class TeamBase : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private GameObject _agentPrefab;
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
}
