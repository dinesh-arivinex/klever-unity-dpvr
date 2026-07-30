using UnityEngine;

public class LockGrabAxis : MonoBehaviour
{
    [Header("Lock Position")]
    public bool lockX;
    public bool lockY;
    public bool lockZ;

    [Header("Position Clamp")]
    public bool clampX;
    public float minX;
    public float maxX;

    public bool clampY;
    public float minY;
    public float maxY;

    public bool clampZ;
    public float minZ;
    public float maxZ;

    [Header("Lock Rotation")]
    public bool lockRotX;
    public bool lockRotY;
    public bool lockRotZ;

    private Vector3 initialLocalPosition;
    private Vector3 initialLocalRotation;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localEulerAngles;
    }

    private void LateUpdate()
    {
        Vector3 pos = transform.localPosition;

        // X
        if (lockX)
            pos.x = initialLocalPosition.x;
        else if (clampX)
            pos.x = Mathf.Clamp(pos.x, minX, maxX);

        // Y
        if (lockY)
            pos.y = initialLocalPosition.y;
        else if (clampY)
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // Z
        if (lockZ)
            pos.z = initialLocalPosition.z;
        else if (clampZ)
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        transform.localPosition = pos;

        Vector3 rot = transform.localEulerAngles;

        if (lockRotX)
            rot.x = initialLocalRotation.x;

        if (lockRotY)
            rot.y = initialLocalRotation.y;

        if (lockRotZ)
            rot.z = initialLocalRotation.z;

        transform.localEulerAngles = rot;
    }

    public void SetCurrentAsLocked()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localEulerAngles;
    }
}