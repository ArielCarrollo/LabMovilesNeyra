using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkSpawner : NetworkBehaviour
{
    [Header("Spawner Settings")]
    [Tooltip("El objeto que se va a generar.")]
    [SerializeField] private GameObject prefabToSpawn;
    [Tooltip("Lista de puntos donde pueden aparecer los objetos.")]
    [SerializeField] private List<Transform> spawnPoints;
    [Tooltip("Con qué frecuencia (en segundos) se genera un nuevo objeto.")]
    [SerializeField] private float spawnInterval = 5f;
    [Tooltip("La cantidad máxima de objetos de este tipo que pueden existir a la vez.")]
    [SerializeField] private int maxSpawnedObjects = 10;

    private List<GameObject> spawnedObjects = new List<GameObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        InvokeRepeating(nameof(SpawnObject), 1f, spawnInterval);
    }

    private void SpawnObject()
    {
        spawnedObjects.RemoveAll(obj => obj == null);

        if (spawnedObjects.Count >= maxSpawnedObjects) return;
        if (spawnPoints.Count == 0) return;

        Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

        GameObject newObject = Instantiate(prefabToSpawn, randomSpawnPoint.position, randomSpawnPoint.rotation);
        newObject.GetComponent<NetworkObject>().Spawn(true);

        spawnedObjects.Add(newObject);
    }
}