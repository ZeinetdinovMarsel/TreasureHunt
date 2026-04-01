using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class TRN_Filters
{
    public static float random(float2 uv)
    {
        float rnd = frac(sin(dot(uv.xy, float2(12.9898f, 78.233f))) * 43758.5453123f);
        return rnd;
    }

    public static float Smoothstep(float In, float Edge, float Smoothness)
    {
        return smoothstep(Edge, Edge + Smoothness, In);
    }

    public static float Ridge(float In)
    {
        In = In * 2;
        In -= 1;
        In = abs(In);
        return In;
    }

    public static float Exponential(float In, float power)
    {
        return pow(In, power);
    }

    public static float Invert(float In)
    {
        return 1 - In;
    }

    public static float Canyons(float In, float threshold, float power)
    {
        if(In <= threshold)
        {
            return pow(In, power);
        }
        else
        {
            return In;
        }
    }

    public static float Falloff(Vector2 uv, float width, Vector2 position, float beachHeight)
    {
        // 1. Находим нормализованные координаты от -1 до 1 (0 - центр)
        float halfWidth = width / 2f;
        float x = (position.x - halfWidth) / halfWidth;
        float y = (position.y - halfWidth) / halfWidth;

        // 2. Вместо Vector2.Distance (который дает круг), 
        // берем максимальное значение по одной из осей (дает квадрат)
        float delta = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));

        // Инвертируем: 1 в центре, 0 на краях квадрата
        float falloff = 1 - Mathf.Clamp01(delta);

        // 3. Дальше твоя логика масок, но теперь она работает в границах квадрата
        // Подкрутим пороги, чтобы они не «съедали» слишком много суши
        float mask = Smoothstep(falloff, 0.05f, 0.01f); // Очень близко к краю
        float landmass = Smoothstep(falloff, 0.2f, 0.05f);
        float beaches = Smoothstep(falloff, 0.05f, 0.1f) * beachHeight;

        // Шум для клифов (оставляем как было)
        float cliffs = Mathf.PerlinNoise(uv.x, uv.y);
        cliffs = Smoothstep(cliffs, 0.5f, 0f) * mask;

        float finalFalloff = landmass + beaches + cliffs;

        return pow(Mathf.Clamp01(finalFalloff), 2);
    }
}
