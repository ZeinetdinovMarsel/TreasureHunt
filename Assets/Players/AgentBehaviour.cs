using Cysharp.Threading.Tasks;
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
    [SerializeField] private float _looseSpeedPercent = 0.05f;

    [SerializeField] private LayerMask _agentsLayerMask;
    [SerializeField] private bool _stealAbilityReady;
    [SerializeField] private float _stealRadius;
    public CartBehaviour CartBeh => _cartBeh;
    public float MaxSpeed => _maxSpeed;


    private readonly ReactiveProperty<bool> _isStunned = new(false);
    public UniRx.IReadOnlyReactiveProperty<bool> IsStunned => _isStunned;


    [Inject]
    private void Construct()
    {
        _agent.speed = _maxSpeed;
        _cartBeh.Weight
            .Subscribe(value => ChangeMoveSpeed(value))
            .AddTo(this);
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

    private void ChangeMoveSpeed(float weight)
    {
        int penaltySteps = (int)(weight / 10f);

        float totalPenalty = _maxSpeed * _looseSpeedPercent * penaltySteps;

        _agent.speed = _maxSpeed - totalPenalty;

        if (_agent.speed < 0) _agent.speed = 0;
    }


    public void PickUpItem()
    {
        if (_worldItem != null && !_worldItem.IsPicked)
        {
            _cartBeh.SetObjectOnCart(_worldItem.ItemData, _worldItem);
        }
    }

    public void DropItem()
    {
        _cartBeh.ThrowObjectBack();
        _worldItem = null;
    }

    public void StealItem()
    {
        if (_stealAbilityReady)
        {
            var agentObjects = Physics.OverlapSphere(transform.position + Vector3.up, _stealRadius, _agentsLayerMask);

            foreach (var agentObject in agentObjects)
            {
                if (agentObject.TryGetComponent<AgentBehaviour>(out var agent))
                {
                    var stealObject = agent.CartBeh.RemoveObjectFromCart();
                    if (stealObject != null)
                    {
                        _cartBeh.SetObjectOnCart(_worldItem.ItemData, _worldItem);
                    }
                }
            }
        }
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

    public async UniTask ApplyStunAsync(float duration, CancellationToken token)
    {
        if (_isStunned.Value) return;

        _isStunned.Value = true;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();
        _cartBeh.ThrowObjectBack();

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
