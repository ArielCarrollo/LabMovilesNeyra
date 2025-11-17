using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager genérico que va destruyendo/activando IDestructible
/// de forma aleatoria, entre tiempos min/max.
/// Sirve para minijuegos tipo "piso que se cae", "plataformas que explotan", etc.
/// </summary>
public class HazardRandomDestruction : MonoBehaviour
{
    [Header("Objetos destructibles")]
    [Tooltip("Arrastra aquí componentes que implementen IDestructible (FallingBlock, ProceduralMeshDestruction, etc).")]
    [SerializeField] private List<MonoBehaviour> destructibleBehaviours = new List<MonoBehaviour>();

    [Header("Timing")]
    [SerializeField] private float minTiempoEntreAcciones = 0.5f;
    [SerializeField] private float maxTiempoEntreAcciones = 1.5f;

    [Header("Control")]
    [SerializeField] private bool ejecutarAlActivar = true;

    private List<IDestructible> destructibles = new List<IDestructible>();
    private Coroutine rutina;

    private void Awake()
    {
        destructibles.Clear();

        foreach (var mb in destructibleBehaviours)
        {
            if (mb == null) continue;

            if (mb is IDestructible d)
                destructibles.Add(d);
            else
                Debug.LogWarning($"{mb.name} no implementa IDestructible.", mb);
        }
    }

    private void OnEnable()
    {
        if (ejecutarAlActivar && destructibles.Count > 0)
            rutina = StartCoroutine(RutinaDestruccion());
    }

    private void OnDisable()
    {
        if (rutina != null)
            StopCoroutine(rutina);

        rutina = null;
    }

    /// <summary>
    /// Llamar manualmente desde tu GameManager de rondas.
    /// </summary>
    public void StartHazard()
    {
        if (rutina == null && gameObject.activeInHierarchy && destructibles.Count > 0)
            rutina = StartCoroutine(RutinaDestruccion());
    }

    private IEnumerator RutinaDestruccion()
    {
        var disponibles = new List<IDestructible>(destructibles);

        while (disponibles.Count > 0)
        {
            float espera = Random.Range(minTiempoEntreAcciones, maxTiempoEntreAcciones);
            yield return new WaitForSeconds(espera);

            int index = Random.Range(0, disponibles.Count);
            var d = disponibles[index];

            if (d != null)
                d.TriggerDestruction(); // si quieres, aquí puedes pasar un origin común

            disponibles.RemoveAt(index);
        }

        rutina = null;
    }
}
