using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class RandomPatrol : MonoBehaviour
{
    [Header("Patrol Settings")]
    [SerializeField] private float _patrolRadius = 15f;
    [SerializeField] private float _waitTime = 2f;
    [SerializeField] private float _stoppingDistance = 0.2f;

    private NavMeshAgent _agent;
    private float _timer;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        // Если агент еще идет к цели, сбрасываем таймер
        if (_agent.pathPending || _agent.remainingDistance > _stoppingDistance)
        {
            return;
        }

        // Логика ожидания в точке
        _timer += Time.deltaTime;
        if (_timer >= _waitTime)
        {
            Vector3 newTarget = GetRandomPoint(transform.position, _patrolRadius);
            _agent.SetDestination(newTarget);
            _timer = 0;
        }
    }

    /// <summary>
    /// Ищет ближайшую валидную точку на NavMesh в заданном радиусе.
    /// </summary>
    private Vector3 GetRandomPoint(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;

        NavMeshHit hit;
        // NavMesh.SamplePosition ищет ближайшую точку на поверхности NavMesh
        // Параметр 1 << 0 означает "All Areas" (по умолчанию слой Walkable)
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return center; // Если точку не нашли, остаемся на месте
    }

    // Визуализация радиуса патрулирования в Editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _patrolRadius);
    }
}