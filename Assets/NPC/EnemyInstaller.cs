using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemyInstaller : MonoInstaller
{
    [SerializeField] private NavMeshAgent _agent;
    public override void InstallBindings()
    {
        Container.Bind<NavMeshAgent>().FromInstance(_agent).AsSingle();
    }
}
