using System.Collections.Generic;
using UnityEngine;

public class TreasureGenerator : MonoBehaviour
{
    [SerializeField] private Terrain _terrain;
    [SerializeField] private Vector2 _areaSize = new Vector2(100f, 100f);
    [Range(0f, 1f)][SerializeField] private float _jitter = 0.8f;

    [SerializeField] private TreasureToSpawn[] _treasuresToSpawn;
    [SerializeField] private Transform _container;

    private void Start()
    {
        if (_terrain == null) _terrain = Terrain.activeTerrain;

        GenerateTreasuresUniformly();
    }

    private void GenerateTreasuresUniformly()
    {
        List<GameObject> prefabsToSpawn = new List<GameObject>();
        foreach (var t in _treasuresToSpawn)
        {
            for (int i = 0; i < t.SpawnCount; i++)
            {
                prefabsToSpawn.Add(t.TreasureData.Prefab);
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

                Instantiate(prefabsToSpawn[spawnedCount], spawnPos, Quaternion.Euler(0, Random.Range(0, 360), 0), _container);

                spawnedCount++;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(_areaSize.x, 2f, _areaSize.y));
    }
}

[System.Serializable]

public class TreasureToSpawn
{
    [SerializeField] private ItemData _treasureData;
    [SerializeField] private int _spawnCount;

    public ItemData TreasureData => _treasureData;
    public int SpawnCount => _spawnCount;
}
