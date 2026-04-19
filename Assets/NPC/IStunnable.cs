using Cysharp.Threading.Tasks;
using System.Threading;
using UniRx;

public interface IStunnable
{
    IReadOnlyReactiveProperty<bool> IsStunned { get; }
    UniTask ApplyStunAsync(float duration, CancellationToken token);
}