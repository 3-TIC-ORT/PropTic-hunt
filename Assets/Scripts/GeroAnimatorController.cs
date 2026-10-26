using UnityEngine;

public class GeroAnimationController : MonoBehaviour
{
    private Animator animator;
    private Transform movementRoot;
    private Vector3 lastPosition;

    [SerializeField] private float referenceSpeed = 5f;
    [SerializeField] private float smoothTime = 0.1f;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);

        // El modelo Gero está dentro del objeto Player.
        movementRoot = transform.parent != null
            ? transform.parent
            : transform;

        lastPosition = movementRoot.position;
    }

    private void Update()
    {
        if (animator == null || movementRoot == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.001f);

        // Calculamos cuánto se movió el jugador.
        Vector3 worldVelocity =
            (movementRoot.position - lastPosition) / deltaTime;

        // Convertimos el movimiento a los ejes del jugador.
        Vector3 localVelocity =
            movementRoot.InverseTransformDirection(worldVelocity);

        float moveX = Mathf.Clamp(
            localVelocity.x / referenceSpeed, -1f, 1f
        );

        float moveY = Mathf.Clamp(
            localVelocity.z / referenceSpeed, -1f, 1f
        );

        // Evitamos pequeñas vibraciones cuando está quieto.
        if (new Vector2(moveX, moveY).magnitude < 0.03f)
        {
            moveX = 0f;
            moveY = 0f;
        }

        animator.SetFloat(
            "MoveX", moveX, smoothTime, deltaTime
        );

        animator.SetFloat(
            "MoveY", moveY, smoothTime, deltaTime
        );

        lastPosition = movementRoot.position;
    }
}