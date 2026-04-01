using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

[RequireComponent(typeof(TRN_Erosion))]
public class TRN_Generator : MonoBehaviour
{
    [Header("Size")]
    public int width = 4000;
    [SerializeField] private float height = 250;

    [Header("Noise")]
    [SerializeField] private float scale = 5;
    [SerializeField] private bool rivers = true;
    [SerializeField] private float riverScale = 1;
    [SerializeField] private NoiseStructure[] noise;
    [Range(0, 1)]
    [SerializeField] private float beachHeight = 0.35f;

    public float seed = 0;

    [Header("Objects")]
    [SerializeField] private TRNTerrainObject[] prefabs;
    [SerializeField] private TRNDetaiTexture[] detailTextures;
    [HideInInspector] public bool spawnPrefabs = true;

    Terrain terrain;
    float[,] heightMap;

    public void Generate()
    {
        terrain = GetComponent<Terrain>();
        terrain.terrainData.size = new Vector3(width, height, width);
        terrain.drawHeightmap = true;

        int heightSize = terrain.terrainData.heightmapResolution;

        // NativeArrays для Job System
        NativeArray<float> nativeHeightMap = new NativeArray<float>(heightSize * heightSize, Allocator.TempJob);

        // Конвертируем классы шумов и фильтров в blittable структуры для передачи в Job
        int totalFilters = 0;
        for (int i = 0; i < noise.Length; i++)
        {
            totalFilters += noise[i].filters.Length;
        }

        NativeArray<JobNoiseStructure> nativeNoise = new NativeArray<JobNoiseStructure>(noise.Length, Allocator.TempJob);
        NativeArray<JobNoiseFilter> nativeFilters = new NativeArray<JobNoiseFilter>(totalFilters, Allocator.TempJob);

        int filterIndex = 0;
        for (int i = 0; i < noise.Length; i++)
        {
            nativeNoise[i] = new JobNoiseStructure
            {
                noiseType = noise[i].noiseType,
                scale = noise[i].scale,
                octaves = noise[i].octaves,
                persistence = noise[i].persistence,
                lacunarity = noise[i].lacunarity,
                weight = noise[i].weight,
                filterStartIndex = filterIndex,
                filterCount = noise[i].filters.Length
            };

            for (int j = 0; j < noise[i].filters.Length; j++)
            {
                nativeFilters[filterIndex++] = new JobNoiseFilter
                {
                    filterType = noise[i].filters[j].filterType,
                    exponent = noise[i].filters[j].exponent,
                    step = noise[i].filters[j].step,
                    smoothness = noise[i].filters[j].smoothness,
                    threshold = noise[i].filters[j].threshold,
                    power = noise[i].filters[j].power
                };
            }
        }

        // Запускаем распараллеленную генерацию
        GenerateHeightmapJob genJob = new GenerateHeightmapJob
        {
            heightSize = heightSize,
            seed = seed,
            width = width,
            scale = scale,
            rivers = rivers,
            riverScale = riverScale,
            beachHeight = beachHeight,
            noiseStructures = nativeNoise,
            noiseFilters = nativeFilters,
            heightMap = nativeHeightMap
        };

        // 64 - размер батча. Можно поэкспериментировать, но 64 обычно дает хороший баланс
        genJob.Schedule(heightSize * heightSize, 64).Complete();

        // Возвращаем данные обратно в двумерный массив
        heightMap = new float[heightSize, heightSize];
        for (int x = 0; x < heightSize; x++)
        {
            for (int z = 0; z < heightSize; z++)
            {
                heightMap[x, z] = nativeHeightMap[x * heightSize + z];
            }
        }

        nativeHeightMap.Dispose();
        nativeNoise.Dispose();
        nativeFilters.Dispose();

        terrain.terrainData.SetHeights(0, 0, heightMap);

        GetComponent<TRN_Erosion>().Run();
        Smooth();
        Smooth();
        Smooth();
        Smooth();

        if (spawnPrefabs)
        {
            SpawnDetailTextures();
            SpawnTrees();
        }

        terrain.terrainData.size = new Vector3(width - 1, height, width);
        terrain.terrainData.size = new Vector3(width, height, width);
    }

    public void Smooth()
    {
        terrain = GetComponent<Terrain>();
        int heightSize = terrain.terrainData.heightmapResolution;
        float[,] _heightMap = terrain.terrainData.GetHeights(0, 0, heightSize, heightSize);

        NativeArray<float> input = new NativeArray<float>(heightSize * heightSize, Allocator.TempJob);
        NativeArray<float> output = new NativeArray<float>(heightSize * heightSize, Allocator.TempJob);

        // Переносим данные в плоский NativeArray
        for (int x = 0; x < heightSize; x++)
        {
            for (int z = 0; z < heightSize; z++)
            {
                input[x * heightSize + z] = _heightMap[x, z];
            }
        }

        SmoothJob smoothJob = new SmoothJob
        {
            heightSize = heightSize,
            inputHeightMap = input,
            outputHeightMap = output
        };

        smoothJob.Schedule(heightSize * heightSize, 64).Complete();

        // Возвращаем данные обратно в формат Terrain
        float[,] smoothedHeightmap = new float[heightSize, heightSize];
        for (int x = 0; x < heightSize; x++)
        {
            for (int z = 0; z < heightSize; z++)
            {
                smoothedHeightmap[x, z] = output[x * heightSize + z];
            }
        }

        input.Dispose();
        output.Dispose();

        terrain.terrainData.SetHeights(0, 0, smoothedHeightmap);
    }

    public void SpawnTrees()
    {
        terrain = GetComponent<Terrain>();

        List<TreeInstance> trees = new List<TreeInstance>();

        TreePrototype[] prototypes = new TreePrototype[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            TreePrototype proto = new TreePrototype();
            proto.prefab = prefabs[i].prefab;
            prototypes[i] = proto;
        }
        terrain.terrainData.treePrototypes = prototypes;

        for (int i = 0; i < prefabs.Length; i++)
        {
            TreeInstance tree = new TreeInstance();
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < width; z++)
                {
                    if (x % prefabs[i].spacing == 0 && z % prefabs[i].spacing == 0)
                    {
                        float xOffset = (TRNGen_Heightmap.random(new Vector2(z + seed * i, x + seed * i)) * 2) - 1;
                        float zOffset = (TRNGen_Heightmap.random(new Vector2(x + seed * i, z + seed * i)) * 2) - 1;

                        Vector2 offset = (new Vector2(xOffset, zOffset) * (prefabs[i].spacing / 2));

                        float _x = (float)(x + offset.x) / width;
                        float _z = (float)(z + offset.y) / width;

                        float _y = terrain.terrainData.GetHeight((int)(_x * terrain.terrainData.heightmapResolution), (int)(_z * terrain.terrainData.heightmapResolution)) / (float)height;
                        float grad = terrain.terrainData.GetSteepness(_x, _z);

                        tree.position = new Vector3(_x, _y, _z);
                        tree.heightScale = (TRNGen_Heightmap.random(new Vector2(z + seed, x + seed)) * (prefabs[i].sizeRange.y - prefabs[i].sizeRange.x)) + prefabs[i].sizeRange.x;
                        tree.widthScale = tree.heightScale;

                        tree.prototypeIndex = i;

                        if (_y > prefabs[i].heightRange.x / height && _y < prefabs[i].heightRange.y / height && grad < 20)
                        {
                            trees.Add(tree);
                        }
                    }
                }
            }
        }

        terrain.terrainData.treeInstances = trees.ToArray();

        terrain.terrainData.size = new Vector3(width - 1, height, width);
        terrain.terrainData.size = new Vector3(width, height, width);
    }

    public Vector3 DetailToWorld(int x, int y)
    {
        return new Vector3(
            terrain.GetPosition().x + (((float)x / (float)terrain.terrainData.detailWidth) * (terrain.terrainData.size.x)),
            0f,
            terrain.GetPosition().z + (((float)y / (float)terrain.terrainData.detailHeight) * (terrain.terrainData.size.z))
            );
    }

    public Vector2 GetNormalizedPosition(Vector3 worldPosition)
    {
        Vector3 localPos = terrain.transform.InverseTransformPoint(worldPosition);
        return new Vector2(
            localPos.x / terrain.terrainData.size.x,
            localPos.z / terrain.terrainData.size.z);
    }

    public void SampleHeight(Vector2 position, out float height, out float worldHeight, out float normalizedHeight)
    {
        height = terrain.terrainData.GetHeight(
            Mathf.CeilToInt(position.x * terrain.terrainData.heightmapTexture.width),
            Mathf.CeilToInt(position.y * terrain.terrainData.heightmapTexture.height)
            );

        worldHeight = height + terrain.transform.position.y;
        normalizedHeight = height / terrain.terrainData.size.y;
    }

    public void SpawnDetailTextures()
    {
        terrain = GetComponent<Terrain>();

        List<DetailPrototype> prototypes = new List<DetailPrototype>();
        for (int i = 0; i < detailTextures.Length; i++)
        {
            DetailPrototype proto = new DetailPrototype();
            proto.usePrototypeMesh = false;
            proto.renderMode = DetailRenderMode.Grass;
            proto.healthyColor = Color.white;
            proto.dryColor = Color.white;
            proto.prototypeTexture = detailTextures[i].texture;
            proto.minWidth = detailTextures[i].sizeRange.x;
            proto.maxWidth = detailTextures[i].sizeRange.y;
            proto.minHeight = detailTextures[i].sizeRange.x;
            proto.maxHeight = detailTextures[i].sizeRange.y;
            prototypes.Add(proto);
        }
        terrain.terrainData.detailPrototypes = prototypes.ToArray();

        List<int[,]> detailMaps = new List<int[,]>();

        for (int i = 0; i < detailTextures.Length; i++)
        {
            int[,] map = new int[terrain.terrainData.detailWidth, terrain.terrainData.detailWidth];
            detailMaps.Add(map);
        }

        for (int x = 0; x < terrain.terrainData.detailWidth; x++)
        {
            for (int z = 0; z < terrain.terrainData.detailWidth; z++)
            {
                Vector3 wPos = DetailToWorld(z, x);
                Vector2 normPos = GetNormalizedPosition(wPos);
                SampleHeight(normPos, out _, out wPos.y, out _);

                float grad = terrain.terrainData.GetSteepness(normPos.x, normPos.y);

                for (int i = 0; i < detailTextures.Length; i++)
                {
                    float spacing = detailTextures[i].spacing * 3;
                    int xPos = Mathf.Clamp(x + (int)Random.Range(-spacing, spacing), 1, terrain.terrainData.detailWidth - 1);
                    int zPos = Mathf.Clamp(z + (int)Random.Range(-spacing, spacing), 1, terrain.terrainData.detailWidth - 1);
                    if (wPos.y > detailTextures[i].heightRange.x && wPos.y < detailTextures[i].heightRange.y && grad < 20 && (x % detailTextures[i].spacing == 0 && z % detailTextures[i].spacing == 0))
                    {
                        detailMaps[i][xPos, zPos] = width / 1000;
                    }
                    else
                    {
                        detailMaps[i][xPos, zPos] = 0;
                    }
                }
            }
        }

        for (int i = 0; i < detailMaps.Count; i++)
        {
            terrain.terrainData.SetDetailLayer(0, 0, i, detailMaps[i]);
        }

        terrain.terrainData.size = new Vector3(width - 1, height, width);
        terrain.terrainData.size = new Vector3(width, height, width);
    }
}

// --- СТРУКТУРЫ ДЛЯ JOB SYSTEM ---
public struct JobNoiseFilter
{
    public FilterType filterType;
    public float exponent;
    public float step;
    public float smoothness;
    public float threshold;
    public float power;
}

public struct JobNoiseStructure
{
    public NoiseType noiseType;
    public float scale;
    public int octaves;
    public float persistence;
    public float lacunarity;
    public float weight;
    public int filterStartIndex;
    public int filterCount;
}

// --- JOBS ---
public struct GenerateHeightmapJob : IJobParallelFor
{
    public int heightSize;
    public float seed;
    public int width;
    public float scale;
    public bool rivers;
    public float riverScale;
    public float beachHeight;

    [ReadOnly] public NativeArray<JobNoiseStructure> noiseStructures;
    [ReadOnly] public NativeArray<JobNoiseFilter> noiseFilters;

    public NativeArray<float> heightMap;

    public void Execute(int index)
    {
        // Переводим 1D индекс в 2D координаты
        int x = index / heightSize;
        int z = index % heightSize;

        Vector2 uv = new Vector2(x + seed, z + seed) * width / (50000f * 500f);
        float noiseHeight = 0;
        float total = 0;

        for (int i = 0; i < noiseStructures.Length; i++)
        {
            JobNoiseStructure structure = noiseStructures[i];

            // Важно: TRN_Noise и TRN_Filters методы должны быть статичными и не изменять внешнее состояние.
            float noiseLayer = TRN_Noise.LayeredNoise(structure.noiseType, uv, scale * structure.scale, structure.octaves, structure.persistence, structure.lacunarity);
            total += structure.weight;

            for (int j = 0; j < structure.filterCount; j++)
            {
                JobNoiseFilter filter = noiseFilters[structure.filterStartIndex + j];

                if (filter.filterType == FilterType.Canyon)
                {
                    noiseLayer = TRN_Filters.Canyons(noiseLayer, filter.threshold, filter.power);
                }
                else if (filter.filterType == FilterType.Exponential)
                {
                    noiseLayer = TRN_Filters.Exponential(noiseLayer, filter.exponent);
                }
                else if (filter.filterType == FilterType.Invert)
                {
                    noiseLayer = TRN_Filters.Invert(noiseLayer);
                }
                else if (filter.filterType == FilterType.Ridge)
                {
                    noiseLayer = TRN_Filters.Ridge(noiseLayer);
                }
                else if (filter.filterType == FilterType.Smoothstep)
                {
                    noiseLayer = TRN_Filters.Smoothstep(noiseLayer, filter.step, filter.smoothness);
                }
            }

            noiseHeight += noiseLayer * structure.weight;
        }

        Vector2 riverCoords = (new Vector2(x, z) + new Vector2(seed * 1.5f, seed * 1.5f)) * riverScale;
        float riverMap = rivers ? TRN_Noise.RiverMap(riverCoords, riverScale) : 1f;
        heightMap[index] = (noiseHeight / total) * TRN_Filters.Falloff(uv, heightSize, new Vector2(x, z), beachHeight) * riverMap;
    }
}

public struct SmoothJob : IJobParallelFor
{
    public int heightSize;
    [ReadOnly] public NativeArray<float> inputHeightMap;
    public NativeArray<float> outputHeightMap;

    public void Execute(int index)
    {
        int x = index / heightSize;
        int z = index % heightSize;

        float total = 0;
        int smoothingWidth = 1;

        for (int a = -smoothingWidth; a <= smoothingWidth; a++)
        {
            for (int b = -smoothingWidth; b <= smoothingWidth; b++)
            {
                int xPos = Mathf.Clamp(x + a, 0, heightSize - 1);
                int zPos = Mathf.Clamp(z + b, 0, heightSize - 1);

                total += inputHeightMap[xPos * heightSize + zPos];
            }
        }

        total /= ((smoothingWidth * 2) + 1) * ((smoothingWidth * 2) + 1);
        float currentVal = inputHeightMap[index];
        outputHeightMap[index] = Mathf.Lerp(total, currentVal, 1 - Mathf.Pow(1 - currentVal, 8));
    }
}

// --- ОРИГИНАЛЬНЫЕ ЭНУМЫ И КЛАССЫ ---
public enum FilterType
{
    Ridge,
    Exponential,
    Invert,
    Smoothstep,
    Canyon
}

public enum NoiseType
{
    Perlin,
    Voronoi,
    Edges,
    Chebyshev,
    Manhattan,
}

[System.Serializable]
public class NoiseStructure
{
    [Header("Noise")]
    public NoiseType noiseType;
    public float scale = 1;
    public int octaves = 5;
    public float persistence = 0.5f;
    public float lacunarity = 2;
    [Range(0, 1)]
    public float weight = 1;
    [Header("Filters")]
    public NoiseFilter[] filters;
}

[System.Serializable]
public class NoiseFilter
{
    public FilterType filterType;

    [Header("Exponential Settings")]
    public float exponent;

    [Header("Smoothstep Settings")]
    [Range(0, 1)]
    public float step;
    [Range(0, 2)]
    public float smoothness;

    [Header("Canyon Settings")]
    [Range(0, 1)]
    public float threshold;
    public float power;
}

[System.Serializable]
public struct TRNTerrainObject
{
    public GameObject prefab;
    public int spacing;
    public Vector2 sizeRange;
    public Vector2 heightRange;
}

[System.Serializable]
public struct TRNDetaiTexture
{
    public Texture2D texture;
    public int spacing;
    public Vector2 sizeRange;
    public Vector2 heightRange;
}