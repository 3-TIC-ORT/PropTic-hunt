using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviourPun
{
    [Header("Movimiento")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float deceleration = 18f;

    [Header("Cámara")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Camera playerCamera;

    [Tooltip("Distancia normal de la cámara en tercera persona.")]
    [SerializeField] private float thirdPersonDistance = 5f;

    [Tooltip("Distancia mínima de la cámara.")]
    [SerializeField] private float minimumCameraDistance = 3f;

    [Tooltip("Distancia máxima de la cámara.")]
    [SerializeField] private float maximumCameraDistance = 12f;

    [Tooltip("Altura normal de la cámara.")]
    [SerializeField] private float thirdPersonHeight = 2.2f;

    [Tooltip("Ángulo vertical inicial de la cámara.")]
    [SerializeField] private float thirdPersonAngle = 12f;

    [Tooltip("Espacio extra alrededor del prop.")]
    [SerializeField] private float propCameraPadding = 1.25f;

    [Tooltip("Altura de los ojos del asesino (primera persona).")]
    [SerializeField] private float hunterEyeHeight = 1.7f;

    [Tooltip("Suavidad al cambiar la distancia de cámara.")]
    [SerializeField] private float cameraDistanceSmooth = 5f;

    [Header("Colisión de cámara (escapista)")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private float cameraCollisionMargin = 0.1f;
    [SerializeField] private float minCollisionDistance = 0.3f;
    [SerializeField] private float minPivotHeight = 0.3f;

    [Tooltip("Altura del pivote = altura del prop x este factor (máx. thirdPersonHeight).")]
    [SerializeField] private float heightOverProp = 1.2f;

    [Header("Sensibilidad de cámara")]
    [SerializeField] private float mouseSensitivity = 0.1f;

    [Header("Límites de cámara")]
    [SerializeField] private float hunterMinVerticalAngle = -80f;
    [SerializeField] private float hunterMaxVerticalAngle = 80f;

    [SerializeField] private float escapistMinVerticalAngle = -60f;
    [SerializeField] private float escapistMaxVerticalAngle = 60f;

    [Header("Salto y gravedad")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("Ayuda del salto")]
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.2f;

    private CharacterController controller;

    private float verticalRotation;
    private Vector3 velocity;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private bool isHoldingJump;
    private Vector2 cachedMoveInput;

    private bool isHunter;

    private float currentCameraDistance;

    // Velocidad horizontal actual.
    // Se usa para hacer el movimiento más suave.
    private Vector3 currentHorizontalVelocity;

    // Buffer reutilizable para el SphereCast de la cámara.
    private readonly RaycastHit[] cameraHits = new RaycastHit[16];

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Este Player pertenece a otro cliente.
        if (!photonView.IsMine)
        {
            DisableRemotePlayerCamera();
            enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        DetermineRole();
        SetupCamera();
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        // Con el panel de salida abierto: sin input,
        // pero seguimos aplicando gravedad.
        if (ConfirmarSalida.MenuAbierto || FinDePartida.Terminada)
        {
            cachedMoveInput = Vector2.zero;
            isHoldingJump = false;
            jumpBufferCounter = 0f;

            HandleJump();
            HandleMovement();
            return;
        }

        ReadInput();

        if (isHunter)
        {
            HandleHunterLook();
        }
        else
        {
            HandleEscapistCamera();
        }

        HandleJump();
        HandleMovement();
    }

    // =========================================================
    // ROL
    // =========================================================

    private void DetermineRole()
    {
        if (!RoleManager.HasKiller)
        {
            isHunter = false;
            return;
        }

        isHunter = GameManager.IsLocalAssassin();
    }

    // =========================================================
    // CONFIGURACIÓN DE CÁMARA
    // =========================================================

    private void SetupCamera()
    {
        if (playerCamera == null && cameraHolder != null)
        {
            playerCamera =
                cameraHolder.GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
            return;

        if (isHunter)
        {
            SetupHunterCamera();
        }
        else
        {
            SetupEscapistCamera();
        }
    }

    private void SetupHunterCamera()
    {
        if (cameraHolder == null)
            return;

        // Primera persona.
        // IMPORTANTE: la base del CharacterController está en
        // localPosition.y = 0 (los "pies" del personaje). Si dejamos
        // la cámara en Vector3.zero, queda pegada al piso y el
        // near clip plane corta el suelo. Por eso la subimos
        // a la altura de los ojos.
        cameraHolder.localPosition = new Vector3(0f, hunterEyeHeight, 0f);
        cameraHolder.localRotation = Quaternion.identity;

        playerCamera.transform.localPosition = Vector3.zero;
        playerCamera.transform.localRotation = Quaternion.identity;

        verticalRotation = 0f;
    }

    private void SetupEscapistCamera()
    {
        if (cameraHolder == null)
            return;

        currentCameraDistance = thirdPersonDistance;

        verticalRotation = thirdPersonAngle;

        UpdateEscapistCameraPosition(true);
    }

    // =========================================================
    // CÁMARA DEL PERSEGUIDO
    // =========================================================

    private void HandleEscapistCamera()
    {
        if (cameraHolder == null)
            return;

        Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue();
        }

        float mouseX =
            lookInput.x * mouseSensitivity;

        // Rotación horizontal del jugador (a los lados).
        transform.Rotate(
            Vector3.up * mouseX
        );

        // La cámara del perseguido NO rota verticalmente
        // (arriba/abajo). Se mantiene siempre en el ángulo
        // fijo definido por thirdPersonAngle.
        verticalRotation = thirdPersonAngle;

        UpdateEscapistCameraPosition(false);
    }

    /// <summary>
    /// Calcula los límites combinados de todos los renderers
    /// visibles del jugador (cuerpo o prop actual).
    /// </summary>
    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool found = false;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled)
                continue;

            if (r.GetComponent<Camera>() != null)
                continue;

            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return found;
    }

    private void UpdateEscapistCameraPosition(bool instant)
    {
        float pivotHeight = thirdPersonHeight;
        float desiredDistance = thirdPersonDistance;
        float radius = cameraCollisionRadius;

        if (TryGetBodyBounds(out Bounds bounds))
        {
            // Altura del prop medida desde los pies.
            float top = bounds.max.y - transform.position.y;

            // Prop chico -> pivote bajo.
            // Jugador normal -> sin cambios.
            pivotHeight = Mathf.Clamp(
                top * heightOverProp,
                minPivotHeight,
                thirdPersonHeight
            );

            // Prop grande -> cámara más lejos.
            float largestExtent = Mathf.Max(
                bounds.extents.x,
                bounds.extents.y,
                bounds.extents.z
            );

            desiredDistance = Mathf.Clamp(
                Mathf.Max(
                    thirdPersonDistance,
                    largestExtent * propCameraPadding
                ),
                minimumCameraDistance,
                maximumCameraDistance
            );

            // Esfera más fina para props chicos
            // (si no, no entran bajo una mesa).
            radius = Mathf.Min(
                cameraCollisionRadius,
                Mathf.Max(0.05f, top * 0.25f)
            );
        }

        // ---------------------------------------------------
        // Colisión de cámara
        // ---------------------------------------------------

        Vector3 origin =
            transform.position +
            Vector3.up * pivotHeight;

        Vector3 direction = -transform.forward;

        float allowedDistance = desiredDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            cameraHits,
            desiredDistance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraHits[i];

            // Ignorar los colliders del propio jugador.
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - cameraCollisionMargin)
            );
        }

        allowedDistance = Mathf.Clamp(
            allowedDistance,
            minCollisionDistance,
            desiredDistance
        );

        // Se acerca de golpe (evita ver a través de la pared)
        // y se aleja suavemente.
        if (instant || allowedDistance < currentCameraDistance)
        {
            currentCameraDistance = allowedDistance;
        }
        else
        {
            currentCameraDistance = Mathf.Lerp(
                currentCameraDistance,
                allowedDistance,
                cameraDistanceSmooth * Time.deltaTime
            );
        }

        cameraHolder.localPosition = new Vector3(
            0f,
            pivotHeight,
            -currentCameraDistance
        );

        cameraHolder.localRotation = Quaternion.Euler(
            verticalRotation,
            0f,
            0f
        );

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = Vector3.zero;
            playerCamera.transform.localRotation = Quaternion.identity;
        }
    }

    // =========================================================
    // CÁMARA DEL ASESINO
    // =========================================================

    private void HandleHunterLook()
    {
        if (cameraHolder == null)
            return;

        Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            lookInput =
                Mouse.current.delta.ReadValue();
        }

        float mouseX =
            lookInput.x * mouseSensitivity;

        float mouseY =
            lookInput.y * mouseSensitivity;

        // Girar horizontalmente el jugador.
        transform.Rotate(
            Vector3.up * mouseX
        );

        // Girar verticalmente la cámara.
        verticalRotation -= mouseY;

        verticalRotation =
            Mathf.Clamp(
                verticalRotation,
                hunterMinVerticalAngle,
                hunterMaxVerticalAngle
            );

        cameraHolder.localRotation =
            Quaternion.Euler(
                verticalRotation,
                0f,
                0f
            );
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void ReadInput()
    {
        cachedMoveInput = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed)
            cachedMoveInput.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            cachedMoveInput.y -= 1f;

        if (Keyboard.current.dKey.isPressed)
            cachedMoveInput.x += 1f;

        if (Keyboard.current.aKey.isPressed)
            cachedMoveInput.x -= 1f;

        cachedMoveInput =
            Vector2.ClampMagnitude(
                cachedMoveInput,
                1f
            );

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpBufferCounter =
                jumpBufferTime;
        }

        isHoldingJump =
            Keyboard.current.spaceKey.isPressed;
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    private void HandleMovement()
    {
        Vector3 inputDirection =
            transform.right *
            cachedMoveInput.x;

        inputDirection +=
            transform.forward *
            cachedMoveInput.y;

        // Evita que diagonal sea más rápida.
        inputDirection =
            Vector3.ClampMagnitude(
                inputDirection,
                1f
            );

        Vector3 targetVelocity =
            inputDirection * speed;

        float movementRate;

        if (inputDirection.sqrMagnitude > 0.01f)
        {
            movementRate = acceleration;
        }
        else
        {
            movementRate = deceleration;
        }

        // Aceleración / frenado suave.
        currentHorizontalVelocity =
            Vector3.MoveTowards(
                currentHorizontalVelocity,
                targetVelocity,
                movementRate *
                Time.deltaTime
            );

        Vector3 finalMove =
            currentHorizontalVelocity +
            velocity;

        CollisionFlags collisionFlags =
            controller.Move(
                finalMove *
                Time.deltaTime
            );

        // Si el CharacterController detecta suelo,
        // mantenemos una pequeña velocidad negativa
        // para que permanezca pegado al piso.
        if ((collisionFlags & CollisionFlags.Below) != 0)
        {
            if (velocity.y < 0f)
            {
                velocity.y = -2f;
            }
        }
    }

    // =========================================================
    // SALTO Y GRAVEDAD
    // =========================================================

    private void HandleJump()
    {
        bool grounded =
            controller.isGrounded;

        if (grounded)
        {
            coyoteTimeCounter =
                coyoteTime;
        }
        else
        {
            coyoteTimeCounter -=
                Time.deltaTime;
        }

        if (grounded &&
            velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (jumpBufferCounter > 0f &&
            coyoteTimeCounter > 0f)
        {
            velocity.y =
                Mathf.Sqrt(
                    jumpHeight *
                    -2f *
                    gravity
                );

            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        if (velocity.y < 0f)
        {
            velocity.y +=
                gravity *
                fallMultiplier *
                Time.deltaTime;
        }
        else if (velocity.y > 0f &&
                 !isHoldingJump)
        {
            velocity.y +=
                gravity *
                lowJumpMultiplier *
                Time.deltaTime;
        }
        else
        {
            velocity.y +=
                gravity *
                Time.deltaTime;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -=
                Time.deltaTime;
        }
    }

    // =========================================================
    // JUGADORES REMOTOS
    // =========================================================

    private void DisableRemotePlayerCamera()
    {
        if (playerCamera == null &&
            cameraHolder != null)
        {
            playerCamera =
                cameraHolder.GetComponentInChildren<Camera>();
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        AudioListener listener =
            GetComponentInChildren<AudioListener>();

        if (listener != null)
        {
            listener.enabled = false;
        }
    }
}