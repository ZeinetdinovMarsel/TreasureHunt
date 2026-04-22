using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemyInstaller : MonoInstaller
{
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Animator _animator;
    [SerializeField] private EnemyAI _enemyAI;
    public override void InstallBindings()
    {
        Container.Bind<NavMeshAgent>().FromInstance(_agent).AsSingle();
        Container.Bind<Animator>().FromInstance(_animator).AsSingle();
        Container.Bind<EnemyAI>().FromInstance(_enemyAI).AsSingle();
    }
}
