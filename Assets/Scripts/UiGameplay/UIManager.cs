using UnityEngine;
using UnityEngine.UI; // ¡Importante para el Layout Group!
using System.Collections.Generic;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Prefabs")]
    [SerializeField] private GameObject playerUIPortraitPrefab; // Tu prefab 'PlayerUIPortrait'

    [Header("Contenedores de Layout")]
    [SerializeField] private Transform ffaLayoutContainer; // Para 1v1 y Todos vs Todos
    // [SerializeField] private Transform team1LayoutContainer; // (Para 2v2 después)
    // [SerializeField] private Transform team2LayoutContainer; // (Para 2v2 después)

    // Un diccionario para saber qué UI pertenece a qué jugador
    private Dictionary<ulong, PlayerUIPortrait> playerPortraits = new Dictionary<ulong, PlayerUIPortrait>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Llamado por CharacterBase cuando un jugador ENTRA a la partida.
    /// </summary>
    public void RegisterPlayer(CharacterBase player)
    {
        ulong playerID = player.OwnerClientId;

        // Evitar duplicados
        if (playerPortraits.ContainsKey(playerID)) return;

        // --- Aquí decides dónde ponerlo ---
        // Por ahora, todos van al mismo contenedor
        Transform container = ffaLayoutContainer;

        // (Lógica 2v2 futura iría aquí, ej: if (player.TeamId == 1) container = team1LayoutContainer;)

        // 1. Instanciar el Prefab de UI dentro del contenedor
        GameObject portraitGO = Instantiate(playerUIPortraitPrefab, container);

        // 2. Conectar el script del prefab con el script del jugador
        PlayerUIPortrait portraitScript = portraitGO.GetComponent<PlayerUIPortrait>();
        portraitScript.Initialize(player);

        // 3. Guardar la referencia
        playerPortraits.Add(playerID, portraitScript);
    }

    /// <summary>
    /// Llamado por CharacterBase cuando un jugador SALE de la partida.
    /// </summary>
    public void UnregisterPlayer(CharacterBase player)
    {
        ulong playerID = player.OwnerClientId;

        if (playerPortraits.TryGetValue(playerID, out PlayerUIPortrait portraitScript))
        {
            // Destruir el GameObject de la UI
            Destroy(portraitScript.gameObject);

            // Quitarlo del diccionario
            playerPortraits.Remove(playerID);
        }
    }
}