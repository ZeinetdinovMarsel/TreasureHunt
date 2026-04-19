using PrimeTween;
using sc.terrain.proceduralpainter;
using UniRx;
using UnityEngine;

public class CartBehaviour : MonoBehaviour
{
    [SerializeField] private Transform _placeTransform;
    private ItemData _storedItem;
    private WorldItem _storedObject;
    public ReactiveProperty<float> Weight { get; private set; } = new FloatReactiveProperty(0f);
    public void SetObjectOnCart(ItemData itemToStore, WorldItem objectToStore)
    {
        _storedItem = itemToStore;
        _storedObject = objectToStore;

        objectToStore.MainObject.transform.SetParent(_placeTransform);
        objectToStore.MainObject.transform.localPosition = Vector3.zero;
        objectToStore.MainObject.transform.localScale = Vector3.one;
        objectToStore.MainObject.transform.rotation = Quaternion.identity;

        Weight.Value = itemToStore.Weight;

        Tween.PunchScale(_storedObject.transform, Vector3.one * 0.2f, 0.4f);
    }

    public ItemData RemoveObjectFromCart()
    {
        Tween.Scale(_storedObject.transform, 0, 0.5f, Ease.InOutQuad).OnComplete(() =>
        {
            Destroy(_storedObject.gameObject);
            _storedObject = null;
        });

        ItemData data = _storedItem;
        _storedItem = null;

        Weight.Value = 0;
        return data;
    }
    public void ThrowObjectBack(float distance = 2f, float height = 1.2f, float duration = 0.5f)
    {
        if (_storedObject == null)
            return;

        GameObject obj = _storedObject.MainObject;
        
        obj.transform.SetParent(null);

        Transform t = obj.transform;

        Vector3 startPos = t.position;
        Vector3 backDir = transform.forward;

        Vector3 endPos = FindDropPoint(startPos, backDir, distance);

        Vector3 midPoint = (startPos + endPos) / 2 + Vector3.up * height;

        Weight.Value = 0;

        Sequence seq = Sequence.Create();

        seq.Group(Tween.Position(t, midPoint, duration * 0.5f, Ease.OutQuad));
        seq.Chain(Tween.Position(t, endPos, duration * 0.5f, Ease.InQuad));

        seq.ChainCallback(() =>
        {
            Tween.PunchScale(t, Vector3.one * 0.2f, 0.4f);
        });

        seq.OnComplete(() =>
        {
            _storedObject.IsPicked = false;
            _storedObject = null;
        });
    }
    private Vector3 FindDropPoint(Vector3 start, Vector3 direction, float distance)
    {
        Vector3 target = start + direction * distance;

        RaycastHit hit;

        if (Physics.Raycast(target + Vector3.up * 5f, Vector3.down, out hit, 10f))
        {
            return hit.point;
        }
        return target;
    }
}
