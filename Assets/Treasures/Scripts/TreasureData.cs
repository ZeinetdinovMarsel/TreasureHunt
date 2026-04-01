using UnityEngine;

[CreateAssetMenu(fileName = "TreasureData", menuName = "Treasures/TreasureData")]
public class TreasureData : ScriptableObject
{
    [field: SerializeField] public float Cost { get; private set; }
    [field: SerializeField] public float Weight { get; private set; }
    [field: SerializeField] public GameObject Prefab { get; private set; }
}
