using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            // Busca la c�mara principal solo una vez para ser eficiente.
            mainCameraTransform = Camera.main?.transform;
        }

        if (mainCameraTransform == null) return;

        // Rota el objeto para que su "adelante" apunte en la misma direcci�n que el "adelante" de la c�mara.
        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
            mainCameraTransform.rotation * Vector3.up);
    }
}