using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public enum State { Patrolling, Chasing, Attacking }

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private EnemySettings _settings;
    public EnemySettings Settings => _settings;

    [Inject] private NavMeshAgent _agent;

    private IStunnable _currentTarget;
    private CompositeDisposable _disposables = new();

    private CancellationTokenSource _attackCts;
    private CancellationTokenSource _forgetCts;

    private ReactiveProperty<State> _currentState = new(State.Patrolling);
    private bool _isAttacking;
    private readonly Collider[] _detectionBuffer = new Collider[5];

    public IObservable<Unit> OnAttack => _onAttack;
    private Subject<Unit> _onAttack = new();
    private EnemyAnimationBehaviour _anim;

    private bool _isForgetting;
    [SerializeField] private bool _isWaiting;
    private Vector3 _initialPosition;

    public float CurrentSpeed => _agent != null ? _agent.speed : 0f;

    public string CurrentState => _currentState.Value switch
    {
        State.Patrolling => "Patrol",
        State.Chasing => "Chase",
        State.Attacking => "Attack",
        _ => "Unknown"
    };

    public int? TargetAgentId
    {
        get
        {
            if (_currentTarget is AgentBehaviour agent)
            {
                if (int.TryParse(agent.AgentId, out var id))
                    return id;
            }
            return null;
        }
    }

    private void Awake()
    {
        _initialPosition = transform.position;
    }

    private void Start()
    {
        _agent.speed = _settings.PatrolSpeed;
        _anim = GetComponent<EnemyAnimationBehaviour>();

        _anim.OnAttackHit
           .Subscribe(_ => ApplyDamage())
           .AddTo(_disposables);

        Observable.EveryFixedUpdate()
            .Subscribe(_ => Tick())
            .AddTo(_disposables);

        _currentState.Subscribe(OnStateChanged).AddTo(_disposables);
    }

    private void Tick()
    {
        if (_isAttacking) return;

        if (!_isForgetting)
        {
            SearchForTargets();
        }

        switch (_currentState.Value)
        {
            case State.Patrolling:
                UpdatePatrol().Forget();
                if (_currentTarget != null)
                    _currentState.Value = State.Chasing;
                break;

            case State.Chasing:
                UpdateChase();
                break;
        }
    }

    private void SearchForTargets()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _settings.DetectionRange, _detectionBuffer, _settings.TargetLayer);

        IStunnable bestTarget = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Transform targetTrans = _detectionBuffer[i].transform;
            Vector3 directionToTarget = (targetTrans.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, targetTrans.position);

            if (Vector3.Angle(transform.forward, directionToTarget) < _settings.ViewAngle / 2f)
            {
                if (!Physics.Raycast(transform.position + Vector3.up, directionToTarget, distance, _settings.ObstacleLayer))
                {
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestTarget = targetTrans.GetComponent<IStunnable>();
                    }
                }
            }
        }
        _currentTarget = bestTarget;
    }

    private void UpdateChase()
    {
        if (_currentTarget == null || Vector3.Distance(transform.position, GetTargetPos()) > _settings.StopChaseRange)
        {
            _currentState.Value = State.Patrolling;
            _currentTarget = null;
            return;
        }

        float distance = Vector3.Distance(transform.position, GetTargetPos());
        _agent.SetDestination(GetTargetPos());

        if (distance <= _settings.AttackRange)
        {
            AttackSequenceAsync().Forget();
        }
    }

    private Vector3 GetTargetPos() => ((MonoBehaviour)_currentTarget).transform.position;

    private void ApplyDamage()
    {
        if (_currentTarget == null) return;

        if (_currentTarget is AgentBehaviour victim)
        {
            victim.ApplyStunAsync(victim.GetCancellationTokenOnDestroy()).Forget();
        }

        StartForgetTargetAsync().Forget();
    }

    private async UniTaskVoid StartForgetTargetAsync()
    {
        _forgetCts?.Cancel();
        _forgetCts?.Dispose();

        _forgetCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        _isForgetting = true;
        _currentTarget = null;
        _currentState.Value = State.Patrolling;

        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_settings.ForgetTargetDuration),
                cancellationToken: _forgetCts.Token
            );
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            _isForgetting = false;
        }
    }

    private async UniTaskVoid AttackSequenceAsync()
    {
        if (_currentTarget == null || _isAttacking) return;

        _attackCts?.Cancel();
        _attackCts?.Dispose();
        _attackCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        _isAttacking = true;
        _agent.isStopped = true;

        transform.LookAt(GetTargetPos());
        _onAttack.OnNext(Unit.Default);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_settings.AttackCooldown), cancellationToken: _attackCts.Token);
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (_agent != null) _agent.isStopped = false;
            _isAttacking = false;
        }
    }

    private async UniTaskVoid UpdatePatrol()
    {
        if (_agent != null && !_isWaiting && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            await WaitAtPoint();
            if (_agent != null) _agent.SetDestination(GetRandomNavMeshPoint());
        }
    }

    private async UniTask WaitAtPoint()
    {
        _isWaiting = true;
        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(UnityEngine.Random.Range(1f, 3f)),
                cancellationToken: this.GetCancellationTokenOnDestroy()
            );
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isWaiting = false;
        }
    }

    private Vector3 GetRandomNavMeshPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 random = _initialPosition + UnityEngine.Random.insideUnitSphere * _settings.PatrolRadius;
            if (NavMesh.SamplePosition(random, out var hit, 5f, NavMesh.AllAreas))
                return hit.position;
        }
        return _initialPosition;
    }

    private void OnStateChanged(State newState)
    {
        if (_agent != null)
            _agent.speed = (newState == State.Chasing) ? _settings.ChaseSpeed : _settings.PatrolSpeed;
    }

    private void OnDestroy()
    {
        _disposables.Dispose();

        _attackCts?.Cancel();
        _attackCts?.Dispose();

        _forgetCts?.Cancel();
        _forgetCts?.Dispose();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_settings == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _settings.DetectionRange);

        Gizmos.color = Color.red;
        Vector3 leftBoundary = Quaternion.Euler(0, -_settings.ViewAngle / 2f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, _settings.ViewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, leftBoundary * _settings.DetectionRange);
        Gizmos.DrawRay(transform.position, rightBoundary * _settings.DetectionRange);
    }
#endif
}