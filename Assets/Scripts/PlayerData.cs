using UnityEngine;

// No es un MonoBehaviour, es solo una clase para guardar datos.
[System.Serializable] // Esto permite que Unity pueda "ver" la clase en el inspector si es necesario.
public class PlayerData
{
    public string username;
    public string password;

    // Datos del juego que queremos guardar
    public Vector3 position;
    public int health;
    public int attack;

    // Constructor para crear un nuevo jugador con datos por defecto.
    public PlayerData(string user, string pass)
    {
        username = user;
        password = pass;

        // Valores iniciales para un jugador recién registrado.
        position = new Vector3(0, 1, 0); // Posición inicial de spawn.
        health = 100;
        attack = 10;
    }
}