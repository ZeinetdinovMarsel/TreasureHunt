using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Items/ItemData")]
public class ItemData : ScriptableObject
{
    [field: SerializeField] public float Weight { get; private set; }
    [field: SerializeField] public GameObject Prefab { get; private set; }
}
