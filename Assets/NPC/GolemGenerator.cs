using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class GolemGenerator : GridSpawnerBase<GolemToSpawn>
{
    [Inject] private DiContainer _containerDI;
    [SerializeField] private GolemToSpawn[] _golems;

    protected override IEnumerable<GolemToSpawn> GetSpawnData()
    {
        foreach (var g in _golems)
            for (int i = 0; i < g.SpawnCount; i++)
                yield return g;
    }

    protected override GameObject GetPrefab(GolemToSpawn data)
        => data.GolemPrefab;

    protected override Vector3 AdjustPosition(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;

        return pos;
    }

    protected override GameObject Spawn(GameObject prefab, Vector3 pos)
    {
        return _containerDI.InstantiatePrefab(prefab, pos, Quaternion.identity, _container);
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