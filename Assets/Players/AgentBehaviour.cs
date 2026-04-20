using Cysharp.Threading.Tasks;
using sc.terrain.proceduralpainter;
using System;
using System.Threading;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Zenject;

public class AgentBehaviour : MonoBehaviour, IStunnable
{
    [Inject] CartBehaviour _cartBeh;
    [Inject] NavMeshAgent _agent;

    [SerializeField] private WorldItem _worldItem;
    [SerializeField] private float _maxSpeed = 5;
    [SerializeField] private int _looseWeightStep = 10;
    [SerializeField] private float _looseSpeedPercent = 0.05f;

    [SerializeField] float _chargePerStep = 0.02f;
    [SerializeField] float _stepDelaySec = 1f;
    [SerializeField] private bool _stealAbilityReady = new();
    [SerializeField] private float _stealRadius = 1;
    [SerializeField] private LayerMask _agentsLayerMask;
    public CartBehaviour CartBeh => _cartBeh;
    public float MaxSpeed => _maxSpeed;
    public bool StealAbilityReady => _stealAbilityReady;

    [SerializeField] private ReactiveProperty<float> _stealAbilityPower = new(0);

    private readonly ReactiveProperty<bool> _isStunned = new(false);
    public IReadOnlyReactiveProperty<bool> IsStunned => _isStunned;


    private CancellationTokenSource _chargeCts;
    private bool _isCharging;

    [SerializeField] private string _agentId;
    [SerializeField] private string _teamId;

    public string AgentId => _agentId;
    public string TeamId => _teamId;

    [Inject]
    private void Construct()
    {
        _agent.speed = _maxSpeed;

        _cartBeh.Weight
            .Subscribe(value => OnCartItemChanged(value))
            .AddTo(this);

        _stealAbilityPower
            .Subscribe(value => _stealAbilityReady = value >= 1)
            .AddTo(this);


    }

    public void Initialize(string agentId, string teamId)
    {
        _agentId = agentId;
        _teamId = teamId;
    }


    private void OnCartItemChanged(float weight)
    {
        ChangeMoveSpeed(weight);

        if (weight <= 0f)
        {
            StartCharging();
        }
        else
        {
            StopCharging();
        }
    }

    private void StartCharging()
    {
        if (_isCharging) return;
        if (_stealAbilityPower.Value >= 1f) return;

        _isCharging = true;
        _chargeCts?.Cancel();
        _chargeCts = new CancellationTokenSource();
        ChargeStealAbility(_chargeCts.Token).Forget();
    }

    private void StopCharging()
    {
        if (!_isCharging) return;
        _isCharging = false;
        _chargeCts?.Cancel();
        _chargeCts?.Dispose();
        _chargeCts = null;
    }

    private async UniTaskVoid ChargeStealAbility(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_stealAbilityPower.Value < 1f)
                {
                    _stealAbilityPower.Value += _chargePerStep;
                    if (_stealAbilityPower.Value > 1f)
                        _stealAbilityPower.Value = 1f;
                }
                else
                {
                    break;
                }
                await UniTask.Delay(TimeSpan.FromSeconds(_stepDelaySec), cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isCharging = false;
        }
    }


    private void ChangeMoveSpeed(float weight)
    {
        int penaltySteps = (int)(weight / _looseWeightStep);

        float totalPenalty = _maxSpeed * _looseSpeedPercent * penaltySteps;

        _agent.speed = _maxSpeed - totalPenalty;

        if (_agent.speed < 0) _agent.speed = 0;
    }
    public void SafeMove(Vector3 offset)
    {
        if (_isStunned.Value) return;
        _agent.SetDestination(_agent.transform.position + offset * 100);
    }

    public void SafeSetDestination(Vector3 target)
    {
        if (_isStunned.Value) return;
        _agent.SetDestination(target);
    }


    public void PickUpItem()
    {
        if (_isStunned.Value) return;
        if (_worldItem != null && !_worldItem.IsPicked)
        {
            _cartBeh.SetObjectOnCart(_worldItem.ItemData, _worldItem);
        }
    }

    public void DropItem()
    {
        if (_isStunned.Value) return;
        _cartBeh.ThrowObjectBack();
        _worldItem = null;
    }

    public void StealItem()
    {
        if (!StealAbilityReady) return;
        if (_isStunned.Value) return;

        var hitColliders = Physics.OverlapSphere(transform.position + Vector3.up,
                                                 _stealRadius,
                                                 _agentsLayerMask,
                                                 QueryTriggerInteraction.Ignore);
        foreach (var col in hitColliders)
        {
            if (col.TryGetComponent<AgentBehaviour>(out var victim))
            {
                if (victim.CompareTag(gameObject.tag)) continue;

                (var stolenItem, var stolenObj) = victim.CartBeh.RemoveObjectFromCart(true);
                if (stolenItem != null)
                {
                    _cartBeh.SetObjectOnCart(stolenItem, stolenObj);
                    _stealAbilityPower.Value = 0f;

                    ApplySpeedBuff().Forget();

                    victim.ApplyStunAsync(this.GetCancellationTokenOnDestroy()).Forget();
                    break;
                }
            }
        }
    }

    private async UniTaskVoid ApplySpeedBuff(float duration = 5)
    {
        float originalWeight = _cartBeh.Weight.Value;
        _agent.speed = _maxSpeed;
        await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());
        ChangeMoveSpeed(_cartBeh.Weight.Value);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Treasure"))
        {
            if (other.TryGetComponent<WorldItem>(out var item))
            {
                _worldItem = item;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Treasure"))
        {
            if (other.TryGetComponent<WorldItem>(out var item))
            {
                _worldItem = null;
            }
        }
    }

    public async UniTask ApplyStunAsync(CancellationToken token, float duration = 5)
    {
        if (_isStunned.Value) return;

        _isStunned.Value = true;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();
        _cartBeh.ThrowObjectBack();
        _worldItem = null;

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
        }
        finally
        {
            _isStunned.Value = false;
            _agent.isStopped = false;
        }
    }
}
