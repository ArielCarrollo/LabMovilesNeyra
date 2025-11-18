using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public struct PlayerScore : INetworkSerializable, System.IEquatable<PlayerScore>
{
    public ulong PlayerId;
    public int Score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Score);
    }

    public bool Equals(PlayerScore other)
    {
        
        return PlayerId == other.PlayerId && Score == other.Score;
    }
}
public class MinigameManager : NetworkBehaviour
{
    public static MinigameManager Instance { get; private set; }


    [SerializeField, Tooltip("Puntos por segundo por tener la corona")]
    private int pointsPerSecond = 1;

    public NetworkVariable<ulong> CurrentKingId = new NetworkVariable<ulong>(999);
    public NetworkList<PlayerScore> PlayerPoints = new NetworkList<PlayerScore>();

    private Dictionary<ulong, CharacterBase> playerList = new Dictionary<ulong, CharacterBase>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); }
        else { Instance = this; }
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!IsServer) return;

        ulong playerID = player.OwnerClientId;

        // 1. Registramos al jugador en el Diccionario
        if (!playerList.ContainsKey(playerID))
        {
            playerList.Add(playerID, player);
            Debug.Log($"MinigameManager: Player {playerID} registrado.");

            // Le creamos su puntaje inicial
            PlayerPoints.Add(new PlayerScore { PlayerId = playerID, Score = 0 });
        }

        // 2. --- LÓGICA DE INICIO AUTOMÁTICO ---
        // Si NO HAY REY (999) o el rey actual ya no existe...
        // ...¡Este nuevo jugador se convierte en el Rey automáticamente!
        if (CurrentKingId.Value == 999 || !playerList.ContainsKey(CurrentKingId.Value))
        {
            Debug.Log("MinigameManager: No había rey. ¡El nuevo jugador es el Rey!");
            TransferCrown(player);
        }
    }

    public void UnregisterPlayer(CharacterBase player)
    {
        if (!IsServer) return;

        ulong playerID = player.OwnerClientId;
        if (playerList.ContainsKey(playerID))
        {
            playerList.Remove(playerID);
        }

        // Opcional: Si el Rey se desconecta, la corona queda "en el limbo" (999)
        // hasta que alguien golpee a alguien o entre otro jugador.
        if (CurrentKingId.Value == playerID)
        {
            CurrentKingId.Value = 999;
        }
    }

    public override void OnNetworkSpawn()
    {

        Debug.Log("MinigameManager: ¡OnNetworkSpawn EJECUTADO! Soy Servidor? " + IsServer);

        if (!IsServer) return;

        StartCoroutine(PointAwardCoroutine());
    }

    private IEnumerator StartMinigameDelay()
    {
        yield return new WaitForSeconds(3.0f);

        if (playerList.Count > 0)
        {
            List<CharacterBase> players = new List<CharacterBase>(playerList.Values);
            CharacterBase firstKing = players[0];

            if (firstKing != null)
            {
                TransferCrown(firstKing);
            }
        }
        else
        {
            Debug.LogError("MinigameManager: ¡No hay jugadores registrados! No se puede asignar la corona.");
        }

        StartCoroutine(PointAwardCoroutine());
    }

    public void TransferCrown(CharacterBase newKing)
    {
        if (!IsServer) return;
        if (newKing == null) return;

        ulong newKingId = newKing.OwnerClientId;

        // Apaga la corona del rey anterior
        if (playerList.TryGetValue(CurrentKingId.Value, out CharacterBase oldKing))
        {
            if (oldKing != null)
            {
                oldKing.IsKing.Value = false;
            }
        }

        // Enciende la corona del nuevo rey
        newKing.IsKing.Value = true;

        // Actualiza el ID del rey
        CurrentKingId.Value = newKingId;
        Debug.Log($"Servidor: ¡La corona pasa a Player {newKingId}!");
    }


    private IEnumerator PointAwardCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);

            ulong kingId = CurrentKingId.Value;

            // Si no hay rey valido (999), esperamos
            if (kingId == 999) continue;

            bool scoreUpdated = false;

            // Buscamos al rey en la lista de puntajes
            for (int i = 0; i < PlayerPoints.Count; i++)
            {
                if (PlayerPoints[i].PlayerId == kingId)
                {
                    PlayerScore score = PlayerPoints[i];
                    score.Score += pointsPerSecond;
                    PlayerPoints[i] = score; // Esto actualiza la UI
                    scoreUpdated = true;
                    break;
                }
            }

            // --- AUTO-REPARACIÓN ---
            // Si el rey existe pero no tenía puntaje (por el bug de inicio), lo creamos ahora.
            if (!scoreUpdated)
            {
                Debug.LogWarning($"Auto-Repair: Creando puntaje para el Rey {kingId}");
                PlayerPoints.Add(new PlayerScore { PlayerId = kingId, Score = pointsPerSecond });
            }
        }
    }
    public int GetPlayerScore(ulong playerId)
    {
        // Busca al jugador en la lista de puntajes
        foreach (PlayerScore scoreEntry in PlayerPoints)
        {
            if (scoreEntry.PlayerId == playerId)
            {
                return scoreEntry.Score; // Devuelve su puntaje
            }
        }

        // Si no lo encuentra, devuelve 0
        return 0;
    }
}