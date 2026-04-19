using UnityEngine;
using System;

[Serializable]
public class EnemySettings
{
    [field: SerializeField] public float PatrolSpeed { get; private set; } = 3.5f;
    [field: SerializeField] public float ChaseSpeed { get; private set; } = 6f;

    [field: SerializeField] public float DetectionRange { get; private set; } = 10f;
    [field: SerializeField] public float StopChaseRange { get; private set; } = 15f;
    [field: SerializeField] public float AttackRange { get; private set; } = 2f;

    [field: SerializeField] public float StunDuration { get; private set; } = 3f;
    [field: SerializeField] public float AttackCooldown { get; private set; } = 2f;


    [field: SerializeField] public LayerMask TargetLayer { get; private set; }
    [field: SerializeField] public LayerMask ObstacleLayer { get; private set; }
    [field: SerializeField] public float ViewAngle { get; private set; } = 90f;
}