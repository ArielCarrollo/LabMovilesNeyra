using System.Collections; // Necesario para la corrutina
using UnityEngine;

// ponle BoxCollider y Rigidbody 3D al objeto
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class FallingBlock : MonoBehaviour
{
    // [SerializeField] private float destruirDespues = 4f; // REEMPLAZADO
    [SerializeField] private float tiempoParaDesactivar = 1.5f; // NUEVO

    private Rigidbody rb;
    private bool yaCayo;
    public bool YaCayo => yaCayo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;      // quieto
        rb.isKinematic = true;      // no se mueve hasta que lo suelto
    }

    public void HacerCaer()
    {
        if (yaCayo) return;
        yaCayo = true;

        rb.isKinematic = false;
        rb.useGravity = true;

        // opcional: para que caiga recto sin voltear
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        // Destroy(gameObject, destruirDespues); // REEMPLAZADO

        // Inicia la corrutina para desactivar el bloque después del tiempo
        StartCoroutine(DesactivarDespuesDeCaer());
    }

    // NUEVA CORRUTINA
    private IEnumerator DesactivarDespuesDeCaer()
    {
        // Espera el tiempo que indicaste (1.5 segundos)
        yield return new WaitForSeconds(tiempoParaDesactivar);

        // Desactiva el objeto en lugar de destruirlo
        gameObject.SetActive(false);
    }
}
