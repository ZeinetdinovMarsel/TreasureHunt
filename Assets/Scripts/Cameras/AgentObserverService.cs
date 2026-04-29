using System.Collections.Generic;
using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Maintains a live list of <see cref="CinemachineCamera"/> components attached to agent
    /// prefabs across all teams. Subscribes to each <see cref="TeamBase.Objects"/> reactive
    /// collection so the list automatically tracks spawns and deaths. Switching the observer
    /// target is just a priority bump on the chosen vcam — no enable/disable juggling.
    /// </summary>
    public sealed class AgentObserverService : IAgentObserverService, IInitializable, System.IDisposable
    {
        private const int ActivePriority = 1200;
        private const int InactivePriority = 0;

        private readonly TeamBase[] _teams;
        private readonly List<CinemachineCamera> _vcams = new List<CinemachineCamera>();
        private readonly Dictionary<AgentBehaviour, CinemachineCamera> _byAgent = new Dictionary<AgentBehaviour, CinemachineCamera>();
        private readonly ReactiveProperty<int> _count = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> _index = new ReactiveProperty<int>(-1);
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        private bool _active;

        public IReadOnlyReactiveProperty<int> Count => _count;
        public IReadOnlyReactiveProperty<int> CurrentIndex => _index;

        public AgentObserverService(TeamBase[] teams)
        {
            _teams = teams ?? System.Array.Empty<TeamBase>();
        }

        public void Initialize()
        {
            foreach (var team in _teams)
            {
                if (team == null) continue;
                team.Objects.ObserveAdd().Subscribe(ev => Add(ev.Value)).AddTo(_disposables);
                team.Objects.ObserveRemove().Subscribe(ev => Remove(ev.Value)).AddTo(_disposables);
                foreach (var existing in team.Objects)
                    Add(existing);
            }
        }

        public void Dispose() => _disposables.Dispose();

        public void Next() => Step(+1);
        public void Previous() => Step(-1);

        public void SelectIndex(int index)
        {
            if (_vcams.Count == 0) { ApplyPriorities(-1); return; }
            int wrapped = ((index % _vcams.Count) + _vcams.Count) % _vcams.Count;
            _index.Value = wrapped;
            ApplyPriorities(wrapped);
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (active && _index.Value < 0 && _vcams.Count > 0)
                _index.Value = 0;
            ApplyPriorities(_active ? _index.Value : -1);
        }

        private void Step(int delta)
        {
            if (_vcams.Count == 0) return;
            int start = Mathf.Max(0, _index.Value);
            SelectIndex(start + delta);
        }

        private void Add(AgentBehaviour agent)
        {
            if (agent == null || _byAgent.ContainsKey(agent)) return;
            var vcam = agent.GetComponentInChildren<CinemachineCamera>(includeInactive: true);
            if (vcam == null || _vcams.Contains(vcam)) return;

            vcam.Priority = InactivePriority;
            _vcams.Add(vcam);
            _byAgent[agent] = vcam;
            _count.Value = _vcams.Count;

            if (_active && _index.Value < 0)
            {
                _index.Value = 0;
                ApplyPriorities(0);
            }
        }

        private void Remove(AgentBehaviour agent)
        {
            // Cull both the explicit mapping and any vcams that have been destroyed alongside
            // their owning prefab so the index never points to a stale entry.
            if (agent != null && _byAgent.TryGetValue(agent, out var vcam))
            {
                _byAgent.Remove(agent);
                int idx = _vcams.IndexOf(vcam);
                if (idx >= 0) RemoveAt(idx);
            }

            for (int i = _vcams.Count - 1; i >= 0; i--)
            {
                if (_vcams[i] == null) RemoveAt(i);
            }
        }

        private void RemoveAt(int i)
        {
            _vcams.RemoveAt(i);
            _count.Value = _vcams.Count;

            if (_vcams.Count == 0)
            {
                _index.Value = -1;
                return;
            }

            if (_index.Value >= _vcams.Count) _index.Value = _vcams.Count - 1;
            if (_active) ApplyPriorities(_index.Value);
        }

        private void ApplyPriorities(int activeIndex)
        {
            for (int i = 0; i < _vcams.Count; i++)
            {
                var v = _vcams[i];
                if (v == null) continue;
                v.Priority = (i == activeIndex) ? ActivePriority : InactivePriority;
            }
        }
    }
}
