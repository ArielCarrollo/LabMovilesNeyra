using UnityEngine;

public class EnemyHealthUI : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Guardamos la referencia a la c�mara para no tener que buscarla en cada frame.
        mainCamera = Camera.main;
    }

    // Usamos LateUpdate para asegurarnos de que la c�mara ya ha completado su movimiento.
    void LateUpdate()
    {
        if (mainCamera == null) return;

        // Esta l�nea hace que el Canvas rote para "mirar" en la direcci�n opuesta a la c�mara.
        // El resultado es que siempre veremos la barra de vida de frente.
        transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
    }
}