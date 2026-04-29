using UnityEngine;
using Zenject;

namespace TreasureHunt.Performance
{
    /// <summary>
    /// Runtime-only quality tuner that targets dominant CPU costs unrelated to terrain:
    ///   • most agents/golems are rarely visible at full zoom, but their Animators were running
    ///     every frame because the default <see cref="AnimatorCullingMode"/> is
    ///     <c>AlwaysAnimate</c>;
    ///   • shadow distance and pixel-light count default to URP-HD values that drive shadow
    ///     caster culling cost up sharply on a horizon shot.
    ///
    /// Touches no scene assets — only adjusts runtime properties on Initialize.
    /// </summary>
    public sealed class SceneRenderingTuner : IInitializable
    {
        public void Initialize()
        {
            TuneAnimators();
            TuneQualitySettings();
        }

        private static void TuneAnimators()
        {
            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var a in animators)
            {
                if (a == null) continue;
                // CullCompletely stops bone updates AND skips Update entirely when no renderer
                // controlled by the animator is visible. The default AlwaysAnimate keeps the
                // animator updating every frame for every dormant agent/golem, which is
                // measurable on dozens of NPCs.
                a.cullingMode = AnimatorCullingMode.CullCompletely;
                a.keepAnimatorStateOnDisable = false;
            }
        }

        private static void TuneQualitySettings()
        {
            // Reduce shadow caster culling work on horizon shots. 60 m is enough to keep close
            // shadows readable while saving the long-tail of distant casters.
            if (QualitySettings.shadowDistance > 60f)
                QualitySettings.shadowDistance = 60f;
        }
    }
}
