using UnityEngine;
using UniRx;

public class WorldItem : MonoBehaviour
{
    [SerializeField] private ItemData _itemData;
    [SerializeField] private Collider _collider;
    [SerializeField] private GameObject _mainObject;

    private readonly BoolReactiveProperty _isPicked = new BoolReactiveProperty(false);
    public string HolderAgentId { get; set; }
    public bool IsPicked
    {
        get => _isPicked.Value;
        set => _isPicked.Value = value;
    }

    public ItemData ItemData => _itemData;
    public GameObject MainObject => _mainObject;

    private void Awake()
    {
        if (_collider == null) _collider = GetComponent<Collider>();
        if (_mainObject == null) _mainObject = gameObject;

        _isPicked
            .Subscribe(picked =>
            {
                if (_collider != null)
                {
                    _collider.enabled = !picked;
                }
            })
            .AddTo(this);
    }
}