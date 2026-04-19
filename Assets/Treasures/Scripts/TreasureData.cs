using UnityEngine;
[CreateAssetMenu(fileName = "TreasureData", menuName = "Items/TreasureData")]
public class TreasureData : ItemData
{
    [field: SerializeField] public float Cost { get; private set; }
}