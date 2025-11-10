using System.Collections;
using UnityEngine;

// pon este script a cada "nube" (objeto 3D con collider)
public class CloudPlatformRespawn : MonoBehaviour
{
    [SerializeField] private string tagMismoTipo = "Cloud";
    [SerializeField] private float tiempoReaparicion = 5f;

    private Vector3 posInicial;
    private Quaternion rotInicial;
    private Collider col;
    private Renderer[] rends;
    private bool ocupada;

    private void Awake()
    {
        posInicial = transform.position;
        rotInicial = transform.rotation;
        col = GetComponent<Collider>();
        rends = GetComponentsInChildren<Renderer>();
    }

    private void OnCollisionEnter(Collision other)
    {
        ProbarDesaparecer(other.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProbarDesaparecer(other);
    }

    private void ProbarDesaparecer(Collider other)
    {
        if (ocupada) return;
        if (other.isTrigger) return;
        if (other.CompareTag(tagMismoTipo)) return; // si es otra nube, no pasa nada

        StartCoroutine(DesaparecerYVolver());
    }

    private IEnumerator DesaparecerYVolver()
    {
        ocupada = true;

        // ocultar
        if (col) col.enabled = false;
        foreach (var r in rends) r.enabled = false;

        yield return new WaitForSeconds(tiempoReaparicion);

        // volver a su sitio
        transform.position = posInicial;
        transform.rotation = rotInicial;

        foreach (var r in rends) r.enabled = true;
        if (col) col.enabled = true;

        ocupada = false;
    }
}
