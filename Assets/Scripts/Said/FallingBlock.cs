using System.Collections;
using UnityEngine;

// ponle BoxCollider y Rigidbody 3D al objeto
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class FallingBlock : MonoBehaviour, IDestructible
{
    [SerializeField] private float tiempoParaDesactivar = 1.5f;

    private Rigidbody rb;
    private bool yaCayo;
    public bool YaCayo => yaCayo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true; // empieza quieto
    }

    /// <summary>
    /// Lógica propia del bloque: se suelta y luego se desactiva.
    /// </summary>
    public void HacerCaer()
    {
        if (yaCayo) return;

        yaCayo = true;
        rb.isKinematic = false;
        rb.useGravity = true;

        StartCoroutine(DesactivarLuego());
    }

    private IEnumerator DesactivarLuego()
    {
        yield return new WaitForSeconds(tiempoParaDesactivar);

        // Aquí puedes cambiar a SetActive(false), o resetear posición si quieres que reaparezca, etc.
        gameObject.SetActive(false);
    }

    // -------- Implementación de IDestructible --------

    void IDestructible.TriggerDestruction()
    {
        HacerCaer();
    }

    void IDestructible.TriggerDestruction(Vector3 origin)
    {
        HacerCaer();

        // Opcional: pequeño empujón extra
        Vector3 dir = (transform.position - origin).normalized;
        if (rb != null && dir != Vector3.zero)
        {
            rb.AddForce(dir * 2f, ForceMode.Impulse);
        }
    }
}
