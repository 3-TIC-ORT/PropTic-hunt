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
        // Configuramos la apariencia del asesino en TODOS los clientes,
        // incluidas las instancias que pertenecen a otros jugadores.
        ConfigureAssassinVisual();

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

    // =========================================================
    // APARIENCIA DEL ASESINO
    // =========================================================

    private void ConfigureAssassinVisual()
    {
        // Si Photon todavía no asignó al dueño, no hacemos nada.
        if (photonView.Owner == null)
            return;

        int assassinActor = RoleManager.GetKillerActorNumber();

        // Solo se cambia la apariencia del jugador que tiene el rol
        // de asesino. Los escapistas conservan su cápsula original.
        if (assassinActor <= 0 ||
            photonView.Owner.ActorNumber != assassinActor)
        {
            return;
        }

        // El archivo del proyecto se llama:
        // Assets/Resources/futurojero.fbx
        // Resources.Load utiliza el nombre sin la extensión.
        GameObject assassinPrefab =
            Resources.Load<GameObject>("futurojero");

        if (assassinPrefab == null)
        {
            Debug.LogError(
                "[PlayerMovement] No se encontró " +
                "Assets/Resources/futurojero.fbx."
            );

            return;
        }

        // Ocultamos la malla de la cápsula solamente para el asesino.
        // No modificamos el CharacterController ni sus colisiones.
        Transform body = transform.Find("Body");

        if (body != null)
        {
            Renderer[] bodyRenderers =
                body.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer bodyRenderer in bodyRenderers)
            {
                if (bodyRenderer != null)
                    bodyRenderer.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning(
                "[PlayerMovement] No se encontró el objeto hijo 'Body'."
            );
        }

        // Creamos el modelo como hijo del jugador para que
        // acompañe sus movimientos y su rotación.
        GameObject assassinModel =
            Instantiate(assassinPrefab, transform, false);

        assassinModel.name = "futuroGero";

        assassinModel.transform.localPosition = Vector3.zero;
        assassinModel.transform.localRotation = Quaternion.identity;
        assassinModel.transform.localScale = Vector3.one;

        // El asesino utiliza primera persona.
        // Ocultamos su modelo solo en su propia pantalla.
        // Los demás jugadores sí podrán verlo.
        if (photonView.IsMine)
        {
            Renderer[] modelRenderers =
                assassinModel.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer modelRenderer in modelRenderers)
            {
                if (modelRenderer != null)
                    modelRenderer.enabled = false;
            }
        }
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
        // La cámara se coloca a la altura de los ojos.
        cameraHolder.localPosition =
            new Vector3(0f, hunterEyeHeight, 0f);

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

        float mouseX = lookInput.x * mouseSensitivity;

        // Rotación horizontal del jugador.
        transform.Rotate(Vector3.up * mouseX);

        // La cámara del perseguido mantiene su ángulo vertical.
        verticalRotation = thirdPersonAngle;

        UpdateEscapistCameraPosition(false);
    }

    /// <summary>
    /// Calcula los límites de los renderers visibles
    /// del jugador o del objeto en el que se transformó.
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
            // Altura del objeto medida desde los pies.
            float top = bounds.max.y - transform.position.y;

            // Objeto pequeño: pivote bajo.
            // Jugador normal: altura habitual.
            pivotHeight = Mathf.Clamp(
                top * heightOverProp,
                minPivotHeight,
                thirdPersonHeight
            );

            // Los objetos grandes necesitan más distancia de cámara.
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

            // Ajuste para objetos pequeños.
            radius = Mathf.Min(
                cameraCollisionRadius,
                Mathf.Max(0.05f, top * 0.25f)
            );
        }

        // ---------------------------------------------------
        // COLISIÓN DE CÁMARA
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
                Mathf.Max(
                    0f,
                    hit.distance - cameraCollisionMargin
                )
            );
        }

        allowedDistance = Mathf.Clamp(
            allowedDistance,
            minCollisionDistance,
            desiredDistance
        );

        // Se acerca de golpe y se aleja suavemente.
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
            lookInput = Mouse.current.delta.ReadValue();
        }

        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Girar horizontalmente el jugador.
        transform.Rotate(Vector3.up * mouseX);

        // Girar verticalmente la cámara.
        verticalRotation -= mouseY;

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            hunterMinVerticalAngle,
            hunterMaxVerticalAngle
        );

        cameraHolder.localRotation = Quaternion.Euler(
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

        cachedMoveInput = Vector2.ClampMagnitude(
            cachedMoveInput,
            1f
        );

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }

        isHoldingJump = Keyboard.current.spaceKey.isPressed;
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    private void HandleMovement()
    {
        Vector3 inputDirection =
            transform.right * cachedMoveInput.x;

        inputDirection +=
            transform.forward * cachedMoveInput.y;

        // Evita que el movimiento diagonal sea más rápido.
        inputDirection = Vector3.ClampMagnitude(
            inputDirection,
            1f
        );

        Vector3 targetVelocity = inputDirection * speed;

        float movementRate;

        if (inputDirection.sqrMagnitude > 0.01f)
        {
            movementRate = acceleration;
        }
        else
        {
            movementRate = deceleration;
        }

        // Aceleración y frenado suaves.
        currentHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetVelocity,
            movementRate * Time.deltaTime
        );

        Vector3 finalMove =
            currentHorizontalVelocity + velocity;

        CollisionFlags collisionFlags = controller.Move(
            finalMove * Time.deltaTime
        );

        // Mantener al jugador pegado al suelo.
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
        bool grounded = controller.isGrounded;

        if (grounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (grounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (jumpBufferCounter > 0f &&
            coyoteTimeCounter > 0f)
        {
            velocity.y = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );

            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        if (velocity.y < 0f)
        {
            velocity.y +=
                gravity * fallMultiplier * Time.deltaTime;
        }
        else if (velocity.y > 0f && !isHoldingJump)
        {
            velocity.y +=
                gravity * lowJumpMultiplier * Time.deltaTime;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    // =========================================================
    // JUGADORES REMOTOS
    // =========================================================

    private void DisableRemotePlayerCamera()
    {
        if (playerCamera == null && cameraHolder != null)
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