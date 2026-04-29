using UniRx;

namespace TreasureHunt.Cameras
{
    /// <summary>
    /// Tracks the agents available for CS:GO-style observer mode and which one is currently
    /// being watched. Implementations are responsible for keeping the list in sync with the
    /// game world (alive agents only) and for ensuring at most one agent vcam is "live" at a
    /// time via Cinemachine priorities.
    /// </summary>
    public interface IAgentObserverService
    {
        /// <summary>Number of agents available to observe.</summary>
        IReadOnlyReactiveProperty<int> Count { get; }

        /// <summary>Index of the currently observed agent, or -1 when none.</summary>
        IReadOnlyReactiveProperty<int> CurrentIndex { get; }

        /// <summary>The currently observed agent (null when none).</summary>
        AgentBehaviour CurrentAgent { get; }

        void Next();
        void Previous();
        void SelectIndex(int index);

        /// <summary>
        /// Activates the observer view (boosts the current agent's vcam priority) when
        /// <paramref name="active"/> is true; deactivates all agent vcams otherwise.
        /// </summary>
        void SetActive(bool active);
    }
}
