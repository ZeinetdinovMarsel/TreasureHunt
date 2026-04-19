using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class AgentBehaviour : MonoBehaviour, IStunnable
{
    [Inject] CartBehaviour _cartBeh;
    [Inject] NavMeshAgent _agent;

    [SerializeField] private WorldItem _worldItem;
    [SerializeField] private float _maxSpeed = 5;
    [SerializeField] private float _looseSpeedPercent=0.05f;

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
        _agent.Move(offset);
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
            _worldItem.IsPicked = true;
            _cartBeh.SetObjectOnCart(_worldItem.ItemData, _worldItem);
        }
    }

    public void DropItem()
    {
        _cartBeh.ThrowObjectBack();
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

    private readonly ReactiveProperty<bool> _isStunned = new(false);
    public IReadOnlyReactiveProperty<bool> IsStunned => _isStunned;

    public async UniTask ApplyStunAsync(float duration, CancellationToken token)
    {
        if (_isStunned.Value) return;

        _isStunned.Value = true;

        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();

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
