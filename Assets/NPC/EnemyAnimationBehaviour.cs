using System;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemyAnimationBehaviour : MonoBehaviour
{
    [Inject] private NavMeshAgent _agent;
    [Inject] private Animator _animator;
    private EnemyAI _enemyAI;

    public IObservable<Unit> OnAttackHit => _onAttackHit;
    private Subject<Unit> _onAttackHit = new();
    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _enemyAI = GetComponent<EnemyAI>();

        Observable.EveryFixedUpdate()
            .Subscribe(_ => UpdateAnim())
            .AddTo(this);

        _enemyAI.OnAttack
           .Subscribe(_ => _animator.SetTrigger("Attack"))
           .AddTo(this);
    }

    void UpdateAnim()
    {
        _animator.SetFloat("MoveSpeed", _agent.velocity.magnitude/_enemyAI.Settings.ChaseSpeed);
    }

    public void Hit()
    {
        _onAttackHit.OnNext(Unit.Default);
    }
}
