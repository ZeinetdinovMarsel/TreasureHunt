using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public abstract class GridSpawnerBase<T> : MonoBehaviour
{
    [SerializeField] protected Terrain _terrain;
    [SerializeField] protected Vector2 _areaSize = new Vector2(100f, 100f);
    [Range(0f, 1f)][SerializeField] protected float _jitter = 0.8f;
    [SerializeField] protected Transform _container;

    [SerializeField] private LayerMask _blockLayer;
    [SerializeField] private float _avoidRadius = 2f;
    [SerializeField] private int _maxRepositionAttempts = 10;

    protected readonly ReactiveCollection<GameObject> _objects = new ReactiveCollection<GameObject>();
    public IReadOnlyReactiveCollection<GameObject> Objects => _objects;

    protected abstract IEnumerable<T> GetSpawnData();
    protected abstract GameObject GetPrefab(T data);
    protected abstract GameObject Spawn(GameObject prefab, Vector3 position);

    protected virtual Vector3 AdjustPosition(Vector3 pos) => pos;

    protected virtual void Awake()
    {
        if (_terrain == null) _terrain = Terrain.activeTerrain;
        SpawnAll();
    }

    protected void SpawnAll()
    {
        var prefabs = new List<GameObject>();

        foreach (var data in GetSpawnData())
            prefabs.Add(GetPrefab(data));

        int totalCount = prefabs.Count;
        if (totalCount == 0) return;

        var buckets = new Dictionary<GameObject, int>();

        foreach (var p in prefabs)
        {
            if (!buckets.ContainsKey(p))
                buckets[p] = 0;

            buckets[p]++;
        }

        var rrList = new List<GameObject>(totalCount);

        while (buckets.Count > 0)
        {
            var keys = new List<GameObject>(buckets.Keys);

            foreach (var key in keys)
            {
                rrList.Add(key);
                buckets[key]--;

                if (buckets[key] <= 0)
                    buckets.Remove(key);
            }
        }

        prefabs = rrList;

        float aspect = _areaSize.x / _areaSize.y;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(totalCount * aspect));
        int rows = Mathf.CeilToInt((float)totalCount / columns);

        float cellSizeX = _areaSize.x / columns;
        float cellSizeZ = _areaSize.y / rows;

        int spawned = 0;

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (spawned >= totalCount) break;

                float posX = (x * cellSizeX) + (cellSizeX / 2f) - (_areaSize.x / 2f);
                float posZ = (z * cellSizeZ) + (cellSizeZ / 2f) - (_areaSize.y / 2f);

                posX += Random.Range(-cellSizeX / 2f, cellSizeX / 2f) * _jitter;
                posZ += Random.Range(-cellSizeZ / 2f, cellSizeZ / 2f) * _jitter;

                Vector3 pos = transform.position + new Vector3(posX, 0, posZ);

                float height = _terrain.SampleHeight(pos);
                pos.y = _terrain.transform.position.y + height;

                pos = AdjustPosition(pos);

                if (!TryFindFreePosition(ref pos, cellSizeX, cellSizeZ))
                {
                    spawned++;
                    continue;
                }

                var obj = Spawn(prefabs[spawned], pos);

                obj.OnDestroyAsObservable()
                    .Subscribe(_ => _objects.Remove(obj))
                    .AddTo(obj);

                _objects.Add(obj);
                spawned++;
            }
        }
    }

    public void Rebuild()
    {
        ClearSpawned();
        SpawnAll();
    }

    private void ClearSpawned()
    {
        var spawned = _objects;
        foreach (var obj in spawned)
        {
            if (obj != null)
                Destroy(obj);
        }

        _objects.Clear();
    }


    private bool IsAreaFree(Vector3 pos)
    {
        return !Physics.CheckSphere(
            pos,
            _avoidRadius,
            _blockLayer
        );
    }

    private bool TryFindFreePosition(ref Vector3 pos, float cellSizeX, float cellSizeZ)
    {
        if (IsAreaFree(pos))
            return true;
 
        Vector3 directionToSpawner = (transform.position - pos).normalized;

        for (int i = 0; i < _maxRepositionAttempts; i++)
        {
            float stepMultiplier = (i + 1) * 0.5f;

            float offsetX = directionToSpawner.x * cellSizeX * stepMultiplier;
            float offsetZ = directionToSpawner.z * cellSizeZ * stepMultiplier;

            offsetX += Random.Range(-cellSizeX * 0.2f, cellSizeX * 0.2f);
            offsetZ += Random.Range(-cellSizeZ * 0.2f, cellSizeZ * 0.2f);

            Vector3 testPos = pos + new Vector3(offsetX, 0, offsetZ);
            float height = _terrain.SampleHeight(testPos);
            testPos.y = _terrain.transform.position.y + height;

            testPos = AdjustPosition(testPos);

            if (IsAreaFree(testPos))
            {
                pos = testPos;
                return true;
            }
        }

        return false;
    }
}