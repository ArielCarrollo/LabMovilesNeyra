using UnityEngine;

public class ScrollTexturaShaderGraph : MonoBehaviour
{
    [Tooltip("Velocidad y dirección del scroll en X e Y.")]
    public Vector2 scrollSpeed = new Vector2(0.1f, 0f); // Puedes dejarlo aquí si quieres control en el Inspector.

    [Tooltip("Nombre de la propiedad Vector2 de velocidad de scroll en el Shader Graph.")]
    public string scrollSpeedProperty = "_ScrollSpeed"; // ¡Importante que coincida con la referencia!

    private Material materialInstance;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError("No se encontró un componente Renderer en este objeto.", this);
            enabled = false;
            return;
        }

        // Creamos una instancia única del material
        materialInstance = rend.material;

        if (materialInstance == null)
        {
            Debug.LogError("Este objeto no tiene material.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (materialInstance == null) return;

        // Si tienes la propiedad _ScrollSpeed en el Shader Graph,
        // este script NO necesita calcular el offset.
        // Solo necesita establecer la velocidad si quieres cambiarla por código.

        // Si solo quieres que el shader maneje el scroll, puedes quitar esta línea.
        // Pero si quieres poder ajustar la velocidad en tiempo real desde C#, déjala.
        materialInstance.SetVector(scrollSpeedProperty, scrollSpeed);
    }

    void OnDestroy()
    {
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}