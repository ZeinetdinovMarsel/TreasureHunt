using System;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class AgentAnimationBehaviour : MonoBehaviour
{
    [Inject] private NavMeshAgent _agent;
    [Inject] private AgentBehaviour _agentBeh;
    [Inject] private Animator _animator;

    private void Start()
    {
        _agentBeh.IsStunned
            .Subscribe(isStunned =>
            {
                _animator.SetBool("IsStunned", isStunned);
            })
            .AddTo(this);

        Observable.EveryFixedUpdate()
            .Subscribe(_ => Tick())
            .AddTo(this);
    }

    private void Tick()
    {
        _animator.SetFloat("MoveSpeed",_agent.velocity.magnitude/_agentBeh.MaxSpeed);
    }

}