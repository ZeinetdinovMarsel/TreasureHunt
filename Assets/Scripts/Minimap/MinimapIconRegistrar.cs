using System.Collections.Generic;
using TreasureHunt.Cameras;
using UniRx;
using UnityEngine;
using Zenject;

namespace TreasureHunt.Minimap
{
    /// <summary>
    /// Subscribes to existing spawners and team bases and automatically attaches a
    /// <see cref="MinimapIcon"/> child to every agent, golem, treasure and base. This keeps
    /// integration zero-touch — no prefab edits required.
    /// </summary>
    public sealed class MinimapIconRegistrar : IInitializable
    {
        private readonly ICameraModeService _modeService;
        private readonly IActiveCameraProvider _cameraProvider;
        private readonly IconTextureFactory _factory;
        private readonly TeamBase[] _teams;
        private readonly GolemGenerator _golems;
        private readonly TreasureGenerator _treasures;

        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly HashSet<int> _registered = new HashSet<int>();

        public MinimapIconRegistrar(
            ICameraModeService modeService,
            IActiveCameraProvider cameraProvider,
            IconTextureFactory factory,
            TeamBase[] teams,
            GolemGenerator golems,
            TreasureGenerator treasures)
        {
            _modeService = modeService;
            _cameraProvider = cameraProvider;
            _factory = factory;
            _teams = teams;
            _golems = golems;
            _treasures = treasures;
        }

        public void Initialize()
        {
            BindTeams();
            BindCollection(_golems.Objects, MinimapIconType.Golem, Color.clear, heightOffset: 4f);
            BindCollection(_treasures.Objects, MinimapIconType.Treasure, Color.clear, heightOffset: 1.5f);
        }

        private void BindTeams()
        {
            if (_teams == null) return;

            foreach (var team in _teams)
            {
                if (team == null) continue;

                Color tint = TeamColor(team.TeamType);
                AttachIfMissing(team.gameObject, MinimapIconType.Base, tint, heightOffset: 6f, visibleInFlyCam: true);

                foreach (var agent in team.Objects)
                    AttachAgentIcon(agent, tint);

                team.Objects.ObserveAdd()
                    .Subscribe(ev => AttachAgentIcon(ev.Value, tint))
                    .AddTo(_disposables);
            }
        }

        private void AttachAgentIcon(AgentBehaviour agent, Color tint)
        {
            if (agent == null) return;
            AttachIfMissing(agent.gameObject, MinimapIconType.Agent, tint, heightOffset: 3.2f);
        }

        private void BindCollection(
            IReadOnlyReactiveCollection<GameObject> collection,
            MinimapIconType type,
            Color tint,
            float heightOffset)
        {
            if (collection == null) return;

            foreach (var go in collection)
                AttachIfMissing(go, type, tint, heightOffset);

            collection.ObserveAdd()
                .Subscribe(ev => AttachIfMissing(ev.Value, type, tint, heightOffset))
                .AddTo(_disposables);
        }

        private void AttachIfMissing(
            GameObject host,
            MinimapIconType type,
            Color tint,
            float heightOffset,
            bool visibleInFlyCam = false)
        {
            if (host == null) return;
            int id = host.GetInstanceID();
            if (!_registered.Add(id)) return;

            var sprite = _factory.GetSprite(type, tint);
            // Icons live at scene root so parent scale/rotation doesn't fight the screen-stable
            // size compensation in MinimapIcon. Lifetime is managed by the icon itself when its
            // tracked target is destroyed.
            var holder = new GameObject($"MinimapIcon_{type}");
            holder.transform.position = host.transform.position;

            var icon = holder.AddComponent<MinimapIcon>();
            icon.Configure(
                target: host.transform,
                sprite: sprite,
                modeService: _modeService,
                cameraProvider: _cameraProvider,
                heightOffset: heightOffset,
                visibleInFlyCam: visibleInFlyCam,
                sortingOrder: SortingOrderFor(type));
        }

        private static int SortingOrderFor(MinimapIconType type)
        {
            return type switch
            {
                MinimapIconType.Base => 1100,
                MinimapIconType.Agent => 1050,
                MinimapIconType.Golem => 1020,
                MinimapIconType.Treasure => 1000,
                _ => 1000
            };
        }

        private static Color TeamColor(TeamType team)
        {
            return team switch
            {
                TeamType.Blue => new Color(0.20f, 0.55f, 1f, 1f),
                TeamType.Red => new Color(1f, 0.30f, 0.30f, 1f),
                _ => Color.white
            };
        }
    }
}
