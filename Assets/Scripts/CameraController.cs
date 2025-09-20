// Nombre del archivo: CameraController.cs
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Transform target; // El jugador a seguir
    [SerializeField] private Vector3 offset = new Vector3(0, 10, -10); // Distancia de la cámara
    [SerializeField] private float smoothSpeed = 0.125f; // Suavidad del seguimiento

    // Lo llamaremos desde el PlayerController cuando spawnee
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Usamos LateUpdate para asegurarnos de que el jugador ya se ha movido
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target.position); // La cámara siempre mira al jugador
    }
}