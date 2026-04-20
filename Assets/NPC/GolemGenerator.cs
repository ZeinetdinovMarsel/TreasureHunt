using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class GolemSpawner : MonoBehaviour
{
    [SerializeField] private Terrain _terrain;
    [SerializeField] private Vector2 _areaSize = new Vector2(100f, 100f);
    [Range(0f, 1f)][SerializeField] private float _jitter = 0.8f;
    [SerializeField] private Transform _container;

    [SerializeField] private GolemToSpawn[] _golemsToSpawn;

    private readonly ReactiveCollection<GameObject> _golems = new ReactiveCollection<GameObject>();
    public IReadOnlyReactiveCollection<GameObject> Golems => _golems;
    [Inject] DiContainer _diContainer;

    private void Start()
    {
        if (_terrain == null) _terrain = Terrain.activeTerrain;
        SpawnGolems();
    }

    private void SpawnGolems()
    {
        List<GameObject> prefabsToSpawn = new List<GameObject>();
        foreach (var golem in _golemsToSpawn)
        {
            for (int i = 0; i < golem.SpawnCount; i++)
            {
                prefabsToSpawn.Add(golem.GolemPrefab);
            }
        }

        int totalCount = prefabsToSpawn.Count;
        if (totalCount == 0) return;

        for (int i = 0; i < totalCount; i++)
        {
            GameObject temp = prefabsToSpawn[i];
            int randomIndex = Random.Range(i, totalCount);
            prefabsToSpawn[i] = prefabsToSpawn[randomIndex];
            prefabsToSpawn[randomIndex] = temp;
        }

        float aspect = _areaSize.x / _areaSize.y;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(totalCount * aspect));
        int rows = Mathf.CeilToInt((float)totalCount / columns);

        float cellSizeX = _areaSize.x / columns;
        float cellSizeZ = _areaSize.y / rows;

        int spawnedCount = 0;

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (spawnedCount >= totalCount) break;

                float posX = (x * cellSizeX) + (cellSizeX / 2f) - (_areaSize.x / 2f);
                float posZ = (z * cellSizeZ) + (cellSizeZ / 2f) - (_areaSize.y / 2f);

                posX += Random.Range(-cellSizeX / 2f, cellSizeX / 2f) * _jitter;
                posZ += Random.Range(-cellSizeZ / 2f, cellSizeZ / 2f) * _jitter;

                Vector3 spawnPos = transform.position + new Vector3(posX, 0, posZ);

                float height = _terrain.SampleHeight(spawnPos);
                spawnPos.y = _terrain.transform.position.y + height;

                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                }
                else
                {
                    spawnedCount++;
                    continue;
                }

                GameObject golemObj = _diContainer.InstantiatePrefab(prefabsToSpawn[spawnedCount], spawnPos, Quaternion.identity, _container);

                golemObj.OnDestroyAsObservable()
                    .Subscribe(_ => _golems.Remove(golemObj))
                    .AddTo(golemObj);

                _golems.Add(golemObj);
                spawnedCount++;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(_areaSize.x, 2f, _areaSize.y));
    }
}

[System.Serializable]
public class GolemToSpawn
{
    [SerializeField] private GameObject _golemPrefab;
    [SerializeField] private int _spawnCount;

    public GameObject GolemPrefab => _golemPrefab;
    public int SpawnCount => _spawnCount;
}