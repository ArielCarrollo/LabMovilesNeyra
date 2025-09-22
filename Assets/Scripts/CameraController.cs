using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Transform target; 
    [SerializeField] private Vector3 offset = new Vector3(0, 10, -10); 
    [SerializeField] private float smoothSpeed = 0.125f; 

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target.position); 
    }
}