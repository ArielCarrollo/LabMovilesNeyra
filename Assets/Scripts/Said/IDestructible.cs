// IDestructible.cs
using UnityEngine;

public interface IDestructible
{
    /// <summary>
    /// Destruye / activa el hazard desde su propio origen.
    /// </summary>
    void TriggerDestruction();

    /// <summary>
    /// Destruye / activa el hazard usando un origen externo
    /// (por ejemplo, centro de explosión).
    /// </summary>
    void TriggerDestruction(Vector3 origin);
}