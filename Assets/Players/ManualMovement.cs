using UnityEngine;
using UnityEngine.AI;
using UniRx;
using Unity.Cinemachine;
using Zenject;

public class AgentManualMovementController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera virtualCamera;

    [Inject] private NavMeshAgent _agent;
    [Inject] private UserInputManager _inputManager;
    [Inject] private AgentBehaviour _agentBehaviour;

    private Vector2 _currentInput = Vector2.zero;

    private void Start()
    {
        BindInput();
    }

    private void BindInput()
    {
        _inputManager.OnMoveAsObservable
            .Subscribe(moveVec => _currentInput = moveVec)
            .AddTo(this);

        _inputManager.OnPickUpItemAsObservable
            .Subscribe(val =>
            {
                if(val == UserInputManager.PressedState.Started)
                {
                    _agentBehaviour.PickUpItem();
                }
            })
            .AddTo(this);

        _inputManager.OnDropItemAsObservable
           .Subscribe(val =>
           {
               if (val == UserInputManager.PressedState.Started)
               {
                   _agentBehaviour.DropItem();
               }
           })
           .AddTo(this);
    }

    private void Update()
    {
        MoveAgent();
    }

    private void MoveAgent()
    {

        if (_currentInput.sqrMagnitude < 0.01f) return;

        Vector3 movementDirection = CalculateCameraRelativeMovement();

        _agentBehaviour.SafeMove(movementDirection * _agent.speed * Time.deltaTime);
        ApplyRotation(movementDirection);
    }

    private Vector3 CalculateCameraRelativeMovement()
    {
        Vector3 forward = virtualCamera.transform.forward;
        Vector3 right = virtualCamera.transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * _currentInput.y + right * _currentInput.x;
        return direction.normalized;
    }

    private void ApplyRotation(Vector3 moveDir)
    {
        if (moveDir == Vector3.zero) return;
        Quaternion lookRotation = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _agent.angularSpeed);
    }
}