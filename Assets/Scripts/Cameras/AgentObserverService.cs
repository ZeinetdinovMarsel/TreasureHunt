using System.Collections.Generic;
using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Maintains a live list of agents (and their <see cref="CinemachineCamera"/> components)
    /// across all teams. Subscribes to each <see cref="TeamBase.Objects"/> reactive collection
    /// so the list automatically tracks spawns and deaths. Switching the observer target is
    /// just a priority bump on the chosen vcam — no enable/disable juggling.
    /// </summary>
    public sealed class AgentObserverService : IAgentObserverService, IInitializable, System.IDisposable
    {
        private const int ActivePriority = 1200;
        private const int InactivePriority = 0;

        private readonly TeamBase[] _teams;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly ReactiveProperty<int> _count = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> _index = new ReactiveProperty<int>(-1);
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        private bool _active;

        public IReadOnlyReactiveProperty<int> Count => _count;
        public IReadOnlyReactiveProperty<int> CurrentIndex => _index;

        public AgentBehaviour CurrentAgent =>
            (_index.Value >= 0 && _index.Value < _entries.Count) ? _entries[_index.Value].Agent : null;

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
            if (_entries.Count == 0) { ApplyPriorities(-1); return; }
            int wrapped = ((index % _entries.Count) + _entries.Count) % _entries.Count;
            _index.Value = wrapped;
            ApplyPriorities(wrapped);
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (active && _index.Value < 0 && _entries.Count > 0)
                _index.Value = 0;
            ApplyPriorities(_active ? _index.Value : -1);
        }

        private void Step(int delta)
        {
            if (_entries.Count == 0) return;
            int start = Mathf.Max(0, _index.Value);
            SelectIndex(start + delta);
        }

        private void Add(AgentBehaviour agent)
        {
            if (agent == null) return;
            if (FindIndex(agent) >= 0) return;

            var vcam = agent.GetComponentInChildren<CinemachineCamera>(includeInactive: true);
            if (vcam == null) return;

            vcam.Priority = InactivePriority;
            // Switch the FreeLook GameObject off so its InputAxisController, OrbitalFollow and
            // RotationComposer stop running every frame on dormant agents — that overhead was
            // contributing to the lag the user reported when looking at the horizon (each of N
            // agents paid its full Cinemachine cost regardless of priority). Only deactivate the
            // GO if it is a *separate* node from the agent root, otherwise we would kill the
            // entire agent.
            bool canDeactivateGo = vcam.gameObject != agent.gameObject;

            _entries.Add(new Entry { Agent = agent, Vcam = vcam, OwnsGameObject = canDeactivateGo });
            _count.Value = _entries.Count;

            ApplyEntryActivation(_entries.Count - 1, isActive: false);

            if (_active && _index.Value < 0)
            {
                _index.Value = 0;
                ApplyPriorities(0);
            }
        }

        private void Remove(AgentBehaviour agent)
        {
            int idx = FindIndex(agent);
            if (idx >= 0) RemoveAt(idx);

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Vcam == null || _entries[i].Agent == null)
                    RemoveAt(i);
            }
        }

        private void RemoveAt(int i)
        {
            _entries.RemoveAt(i);
            _count.Value = _entries.Count;

            if (_entries.Count == 0)
            {
                _index.Value = -1;
                return;
            }

            if (_index.Value >= _entries.Count) _index.Value = _entries.Count - 1;
            if (_active) ApplyPriorities(_index.Value);
        }

        private int FindIndex(AgentBehaviour agent)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Agent == agent) return i;
            return -1;
        }

        private void ApplyPriorities(int activeIndex)
        {
            for (int i = 0; i < _entries.Count; i++)
                ApplyEntryActivation(i, isActive: i == activeIndex);
        }

        private void ApplyEntryActivation(int i, bool isActive)
        {
            var entry = _entries[i];
            var v = entry.Vcam;
            if (v == null) return;

            v.Priority = isActive ? ActivePriority : InactivePriority;
            if (v.enabled != isActive) v.enabled = isActive;

            if (entry.OwnsGameObject && v.gameObject.activeSelf != isActive)
                v.gameObject.SetActive(isActive);
        }

        private struct Entry
        {
            public AgentBehaviour Agent;
            public CinemachineCamera Vcam;
            public bool OwnsGameObject;
        }
    }
}
