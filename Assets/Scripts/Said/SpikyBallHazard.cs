using UnityEngine;

/// <summary>
/// Bola de pinchos que rebota en 3D (vista 2D ortogr�fica),
/// aumenta su velocidad en cada rebote y da�a bloques/jugadores.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class SpikyBallHazard : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float initialSpeed = 5f;
    [SerializeField] private float speedIncreasePerBounce = 0.5f;
    [SerializeField] private float maxSpeed = 15f;
    [Tooltip("Direcci�n inicial en el plano XY.")]
    [SerializeField] private Vector2 initialDirection = Vector2.right;

    [Header("Da�o")]
    [SerializeField] private int damageToPlayer = 1;
    [SerializeField] private int damageToBlocksPerHit = 1;

    private Rigidbody rb;
    private Vector3 lastVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Para que se comporte como 2D:
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY;
    }

    private void OnEnable()
    {
        StartHazard();
    }

    /// <summary>
    /// Llamado al iniciar el modo. Tambi�n puedes llamarlo
    /// manualmente desde tu GameManager de modos.
    /// </summary>
    public void StartHazard()
    {
        Vector3 dir = new Vector3(initialDirection.x, initialDirection.y, 0f).normalized;
        if (dir == Vector3.zero)
            dir = Vector3.right;

        rb.linearVelocity = dir * initialSpeed;
    }

    private void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        // 1) Rebote con reflexi�n y aumento de velocidad
        Vector3 normal = collision.contacts[0].normal;

        float currentSpeed = lastVelocity.magnitude;
        float newSpeed = Mathf.Clamp(currentSpeed + speedIncreasePerBounce, 0.1f, maxSpeed);

        Vector3 dir = Vector3.Reflect(lastVelocity.normalized, normal);

        // Forzar que se quede en el plano XY
        dir.z = 0f;
        dir.Normalize();

        rb.linearVelocity = dir * newSpeed;

        // 2) Da�o a bloques destruibles
        var health = collision.collider.GetComponentInParent<DestructibleHealth>();
        if (health != null)
        {
            health.TakeHit(damageToBlocksPerHit, transform.position);
        }

        // 3) Da�o a jugadores (u otros objetos da�ables)
        //var damageable = collision.collider.GetComponentInParent<IDamageable>();
        //if (damageable != null)
        //{
        //    damageable.TakeDamage(damageToPlayer);
        //}
    }
}
