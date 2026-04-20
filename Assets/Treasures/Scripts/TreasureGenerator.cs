using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
public class TreasureGenerator : GridSpawnerBase<TreasureToSpawn>
{
    [SerializeField] private TreasureToSpawn[] _treasures;

    protected override IEnumerable<TreasureToSpawn> GetSpawnData()
    {
        foreach (var t in _treasures)
            for (int i = 0; i < t.SpawnCount; i++)
                yield return t;
    }

    protected override GameObject GetPrefab(TreasureToSpawn data)
        => data.TreasureData.Prefab;

    protected override GameObject Spawn(GameObject prefab, Vector3 pos)
    {
        return Instantiate(
            prefab,
            pos,
            Quaternion.Euler(0, Random.Range(0, 360), 0),
            _container
        );
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
