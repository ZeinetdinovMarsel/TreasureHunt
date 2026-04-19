using UnityEngine;

public class SimpleWheelSync : MonoBehaviour
{
    [SerializeField] private WheelCollider _leadingWheel;
    [SerializeField] private Transform _wheelsVisualMesh;

    void FixedUpdate()
    {
        float rotationDegrees = (_leadingWheel.rpm * 6f) * Time.fixedDeltaTime;
        _wheelsVisualMesh.Rotate(0, -rotationDegrees, 0, Space.Self);
    }
}