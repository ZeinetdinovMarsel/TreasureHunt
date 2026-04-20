using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UniRx;

public class UserInputManager : MonoBehaviour
{
    private readonly Subject<Vector2> _moveSubject = new Subject<Vector2>();
    private readonly Subject<float> _camHeightSubject = new Subject<float>();
    private readonly Subject<PressedState> _runSubject = new Subject<PressedState>();
    private readonly Subject<PressedState> _pauseSubject = new Subject<PressedState>();
    private readonly Subject<PressedState> _pickUpItemSubject = new Subject<PressedState>();
    private readonly Subject<PressedState> _dropItemSubject = new Subject<PressedState>();
    private readonly Subject<PressedState> _stealItemSubject = new Subject<PressedState>();

    public IObservable<Vector2> OnMoveAsObservable => _moveSubject;
    public IObservable<float> OnCamHeightAsObservable => _camHeightSubject;
    public IObservable<PressedState> OnRunAsObservable => _runSubject;
    public IObservable<PressedState> OnPauseAsObservable => _pauseSubject;
    public IObservable<PressedState> OnPickUpItemAsObservable => _pickUpItemSubject;
    public IObservable<PressedState> OnDropItemAsObservable => _dropItemSubject;
    public IObservable<PressedState> OnStealItemAsObservable => _stealItemSubject;

    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveSubject.OnNext(ctx.ReadValue<Vector2>());
    }
    public void OnHeight(InputAction.CallbackContext ctx)
    {
        _camHeightSubject.OnNext(ctx.ReadValue<float>());
    }
    public void OnRun(InputAction.CallbackContext ctx)
    {
        _runSubject.OnNext(GetPressedState(ctx));
    }

    public void OnPause(InputAction.CallbackContext ctx)
    {
        _pauseSubject.OnNext(GetPressedState(ctx));
    }

    public void OnPickUpItem(InputAction.CallbackContext ctx)
    {
        _pickUpItemSubject.OnNext(GetPressedState(ctx));
    }

    public void OnDropItem(InputAction.CallbackContext ctx)
    {
        _dropItemSubject.OnNext(GetPressedState(ctx));
    }

    public void OnStealItem(InputAction.CallbackContext ctx)
    {
        _stealItemSubject.OnNext(GetPressedState(ctx));
    }

    private PressedState GetPressedState(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled) return PressedState.Canceled;
        if (ctx.started) return PressedState.Started;
        return PressedState.Performed;
    }

    public enum PressedState { Started, Performed, Canceled }

    private void OnDestroy()
    {
        _moveSubject.OnCompleted();
        _camHeightSubject.OnCompleted();
        _runSubject.OnCompleted();
        _pauseSubject.OnCompleted();
        _pickUpItemSubject.OnCompleted();
        _dropItemSubject.OnCompleted();
        _stealItemSubject.OnCompleted();
    }
}