using UnityEngine;

/// <summary>
/// Componente genérico de vida por golpes para objetos que implementan IDestructible.
/// Ejemplo: un bloque que se destruye con 2 o 3 golpes de la bola.
/// </summary>
public class DestructibleHealth : MonoBehaviour
{
    [Tooltip("Cuántos golpes necesita para destruirse.")]
    [SerializeField] private int hitsToDestroy = 2;

    private int currentHits = 0;
    private IDestructible destructible;

    private void Awake()
    {
        // Buscar un componente que implemente IDestructible en este GameObject
        var behaviours = GetComponents<MonoBehaviour>();
        foreach (var mb in behaviours)
        {
            if (mb is IDestructible d)
            {
                destructible = d;
                break;
            }
        }

        if (destructible == null)
        {
            Debug.LogWarning($"[DestructibleHealth] No se encontró IDestructible en {name}. " +
                             $"Si se llega a 0 vida se hará Destroy(gameObject).", this);
        }
    }

    /// <summary>
    /// Llama esto desde la bola u otros hazards cuando golpeen al bloque.
    /// </summary>
    public void TakeHit(int damage, Vector3 hitOrigin)
    {
        if (hitsToDestroy <= 0) return;

        currentHits += Mathf.Max(1, damage);

        if (currentHits >= hitsToDestroy)
        {
            if (destructible != null)
            {
                destructible.TriggerDestruction(hitOrigin);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    public void ResetHealth()
    {
        currentHits = 0;
    }
}
