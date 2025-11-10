using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingLineManager : MonoBehaviour
{
    [SerializeField] private List<FallingBlock> bloques = new List<FallingBlock>();
    [SerializeField] private float minTiempoEntreCaidas = 2.5f;
    [SerializeField] private float maxTiempoEntreCaidas = 5f;

    private void Awake()
    {
        // Si no llenas la lista en el inspector, toma todos los hijos que tengan FallingBlock
        if (bloques.Count == 0)
            bloques.AddRange(GetComponentsInChildren<FallingBlock>());
    }

    private void Start()
    {
        StartCoroutine(CaidasAleatorias());
    }

    private IEnumerator CaidasAleatorias()
    {
        // hacemos copia para ir sacando los que ya cayeron
        var disponibles = new List<FallingBlock>(bloques);

        while (disponibles.Count > 0)
        {
            float espera = Random.Range(minTiempoEntreCaidas, maxTiempoEntreCaidas);
            yield return new WaitForSeconds(espera);

            int i = Random.Range(0, disponibles.Count);
            FallingBlock b = disponibles[i];

            if (b != null && !b.YaCayo)
                b.HacerCaer();

            // ese ya no vuelve
            disponibles.RemoveAt(i);
        }
    }
}
