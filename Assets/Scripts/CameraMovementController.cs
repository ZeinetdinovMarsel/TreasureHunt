using UniRx;
using UniRx.Triggers;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class CameraMovementController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 10;
    [SerializeField] private float _runSpeed = 20;

    [Inject(Id = "UserCam")] private CinemachineCamera _userCamera;
    [Inject] private UserInputManager _userInput;

    private Rigidbody _rigidbody;
    private Vector2 _currentInput;
    private float _currentCamHeight;
    private bool _isRunning;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _rigidbody = GetComponent<Rigidbody>();

        _userInput.OnMoveAsObservable
            .Subscribe(val => _currentInput = val)
            .AddTo(this);

        _userInput.OnCamHeightAsObservable
            .Subscribe(val => _currentCamHeight = val)
            .AddTo(this);

        _userInput.OnRunAsObservable
            .Subscribe(state => _isRunning = state == UserInputManager.PressedState.Performed)
            .AddTo(this);

        this.FixedUpdateAsObservable()
            .Subscribe(_ => ApplyMovement())
            .AddTo(this);
    }

    private void ApplyMovement()
    {
        if (_currentInput.sqrMagnitude < 0.01f && Mathf.Abs(_currentCamHeight) < 0.01f) return;

        Vector3 forward = _userCamera.transform.forward;
        Vector3 right = _userCamera.transform.right;

        Vector3 wishDir = (forward * _currentInput.y + right * _currentInput.x).normalized;

        wishDir.y = _currentCamHeight != 0 ? _currentCamHeight : wishDir.y;

        float speed = _isRunning ? _runSpeed : _moveSpeed;

        _rigidbody.AddForce(wishDir * speed, ForceMode.VelocityChange);
    }
}