using UnityEngine;

public class EnemyHealthUI : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Guardamos la referencia a la cámara para no tener que buscarla en cada frame.
        mainCamera = Camera.main;
    }

    // Usamos LateUpdate para asegurarnos de que la cámara ya ha completado su movimiento.
    void LateUpdate()
    {
        if (mainCamera == null) return;

        // Esta línea hace que el Canvas rote para "mirar" en la dirección opuesta a la cámara.
        // El resultado es que siempre veremos la barra de vida de frente.
        transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
    }
}