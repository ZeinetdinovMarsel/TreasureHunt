using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class AgentInstaller : MonoInstaller
{
    [SerializeField] private CartBehaviour _cartBeh;
    [SerializeField] private Animator _animator;
    [SerializeField] private AgentBehaviour _agentBehaviour;
    [SerializeField] private NavMeshAgent _agent;

    public override void InstallBindings()
    {
        Container.Bind<CartBehaviour>().FromInstance(_cartBeh).AsSingle();
        Container.Bind<Animator>().FromInstance(_animator).AsSingle();
        Container.Bind<AgentBehaviour>().FromInstance(_agentBehaviour).AsSingle();
        Container.Bind<NavMeshAgent>().FromInstance(_agent).AsSingle();
    }
}
