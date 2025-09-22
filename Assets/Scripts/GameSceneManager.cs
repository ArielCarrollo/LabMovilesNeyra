using UnityEngine;
using System.Collections.Generic;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [SerializeField]
    private List<Transform> spawnPoints; // Arrastra aquí los puntos de respawn

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Vector3 GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            // Devuelve una posición por defecto si no hay puntos definidos
            return new Vector3(0, 1, 0);
        }
        return spawnPoints[Random.Range(0, spawnPoints.Count)].position;
    }
}