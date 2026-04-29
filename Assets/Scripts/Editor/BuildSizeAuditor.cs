#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TreasureHunt.EditorTools
{
    /// <summary>
    /// One-stop build-size shrinker. Scans <c>Assets/</c> for the heaviest assets, prints a
    /// summary, and exposes one-click batch operations to apply aggressive but safe import
    /// settings (texture max size, mesh compression, audio compression). All operations are
    /// idempotent and skip third-party demo content.
    ///
    /// The biggest gains in this project come from three places (verified by
    /// <c>du -sh Assets/*</c>):
    ///   1. Lighting bake artefacts under <c>Assets/Scenes/GameScene/</c> (≈ 700 MB) — addressed
    ///      by lowering bake resolution / disabling Adaptive Probe Volumes in the Lighting
    ///      window. This script cannot toggle those scene-level settings safely so it only
    ///      reports them.
    ///   2. Character textures (Kevin Iglesias / Humanoid Giant) at 4K — addressed by
    ///      <c>Tools ▸ Build Size ▸ Compress Character Textures (1K)</c>.
    ///   3. FBX meshes (Coins, Kevin Iglesias) without mesh compression — addressed by
    ///      <c>Tools ▸ Build Size ▸ Enable Mesh Compression</c>.
    /// </summary>
    public static class BuildSizeAuditor
    {
        private const string MenuRoot = "Tools/Build Size/";
        private const long ReportThresholdBytes = 2 * 1024 * 1024; // 2 MB

        // Folders we never touch (third-party demos, plugins). Mesh / texture compression on
        // these would be a noisy diff for no shipping benefit since they aren't in the build.
        private static readonly string[] ExcludedFolders =
        {
            "Assets/TextMesh Pro/Examples & Extras",
            "Assets/Plugins/UniRx/Examples",
            "Assets/Plugins/PrimeTween/Demo",
            "Assets/Plugins/Zenject/OptionalExtras",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Scenes",
            "Assets/Coins/Scenes",
            "Assets/Kevin Iglesias/Characters/Humanoid Giant/Demo Scene",
            "Assets/TutorialInfo",
        };

        [MenuItem(MenuRoot + "Audit (print to console)")]
        public static void Audit()
        {
            var entries = new List<(string path, long size)>();
            foreach (var path in EnumerateAssetFiles())
            {
                try
                {
                    long size = new FileInfo(path).Length;
                    if (size >= ReportThresholdBytes)
                        entries.Add((path, size));
                }
                catch { /* file may be transient; skip */ }
            }

            entries.Sort((a, b) => b.size.CompareTo(a.size));
            long totalCounted = entries.Sum(e => e.size);

            var report = new System.Text.StringBuilder();
            report.AppendLine($"<b>BuildSizeAuditor</b> — top {entries.Count} assets ≥ 2 MB. Total = {ToMb(totalCounted)} MB");
            int showLimit = Mathf.Min(40, entries.Count);
            for (int i = 0; i < showLimit; i++)
            {
                var (path, size) = entries[i];
                report.AppendLine($"  {ToMb(size),8:0.0} MB  {path}");
            }
            if (entries.Count > showLimit)
                report.AppendLine($"  … {entries.Count - showLimit} more");

            Debug.Log(report.ToString());
        }

        [MenuItem(MenuRoot + "Compress Character Textures (1K)")]
        public static void CompressCharacterTextures()
        {
            string[] roots =
            {
                "Assets/Kevin Iglesias",
                "Assets/Free Low Poly Modular Character Pack - Fantasy Dream",
            };
            int touched = ApplyTextureSettings(roots, maxSize: 1024, applyCrunch: true);
            Debug.Log($"BuildSizeAuditor: re-imported {touched} character textures at maxSize=1024 with crunch compression.");
        }

        [MenuItem(MenuRoot + "Compress Environment Textures (2K)")]
        public static void CompressEnvironmentTextures()
        {
            string[] roots =
            {
                "Assets/Handpainted_Grass_and_Ground_Textures",
                "Assets/AllSkyFree",
                "Assets/Proxy Games",
                "Assets/Medieval village",
                "Assets/Blink",
            };
            int touched = ApplyTextureSettings(roots, maxSize: 2048, applyCrunch: true);
            Debug.Log($"BuildSizeAuditor: re-imported {touched} environment textures at maxSize=2048 with crunch compression.");
        }

        [MenuItem(MenuRoot + "Enable Mesh Compression")]
        public static void EnableMeshCompression()
        {
            string[] roots =
            {
                "Assets/Coins",
                "Assets/Kevin Iglesias",
                "Assets/Free Low Poly Modular Character Pack - Fantasy Dream",
                "Assets/Medieval village",
                "Assets/Proxy Games",
                "Assets/Blink",
            };
            int touched = 0;
            foreach (var path in EnumerateAssets(roots, ".fbx", ".obj"))
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                if (mi.meshCompression == ModelImporterMeshCompression.High) continue;
                mi.meshCompression = ModelImporterMeshCompression.High;
                mi.optimizeMeshPolygons = true;
                mi.optimizeMeshVertices = true;
                mi.SaveAndReimport();
                touched++;
            }
            Debug.Log($"BuildSizeAuditor: re-imported {touched} FBX/OBJ meshes with high compression.");
        }

        private static int ApplyTextureSettings(string[] roots, int maxSize, bool applyCrunch)
        {
            int touched = 0;
            foreach (var path in EnumerateAssets(roots, ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".hdr"))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;

                bool changed = false;
                if (ti.maxTextureSize > maxSize) { ti.maxTextureSize = maxSize; changed = true; }
                if (applyCrunch && !ti.crunchedCompression)
                {
                    ti.crunchedCompression = true;
                    ti.compressionQuality = 50;
                    changed = true;
                }
                if (ti.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    changed = true;
                }

                if (changed)
                {
                    ti.SaveAndReimport();
                    touched++;
                }
            }
            return touched;
        }

        private static IEnumerable<string> EnumerateAssetFiles()
        {
            foreach (var path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (IsExcluded(norm)) continue;
                yield return norm;
            }
        }

        private static IEnumerable<string> EnumerateAssets(string[] roots, params string[] extensions)
        {
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string norm = path.Replace('\\', '/');
                    if (norm.EndsWith(".meta", StringComparison.Ordinal)) continue;
                    if (IsExcluded(norm)) continue;
                    foreach (var ext in extensions)
                    {
                        if (norm.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        {
                            yield return norm;
                            break;
                        }
                    }
                }
            }
        }

        private static bool IsExcluded(string path)
        {
            for (int i = 0; i < ExcludedFolders.Length; i++)
            {
                if (path.StartsWith(ExcludedFolders[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static double ToMb(long bytes) => bytes / (1024.0 * 1024.0);
    }
}
#endif
