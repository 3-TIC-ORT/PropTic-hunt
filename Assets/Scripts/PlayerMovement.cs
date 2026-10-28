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

    [Tooltip("Altura de los ojos del asesino.")]
    [SerializeField] private float hunterEyeHeight = 1.7f;

    [Tooltip("Suavidad al cambiar la distancia de cámara.")]
    [SerializeField] private float cameraDistanceSmooth = 5f;

    [Header("Colisión de cámara (escapista)")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private float cameraCollisionMargin = 0.1f;
    [SerializeField] private float minCollisionDistance = 0.3f;
    [SerializeField] private float minPivotHeight = 0.3f;

    [Tooltip("Altura del pivote respecto al prop.")]
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

    [Header("Modelo y animaciones del asesino")]
    [SerializeField] private string assassinModelResource = "futurojero";
    [SerializeField] private string assassinAnimatorControllerResource = "GeroController";
    [SerializeField] private float animationReferenceSpeed = 5f;
    [SerializeField] private float animationSmoothTime = 0.1f;

    private CharacterController controller;

    private float verticalRotation;
    private Vector3 velocity;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private bool isHoldingJump;
    private Vector2 cachedMoveInput;

    private bool isHunter;
    private bool movementLocked = false;

    private float currentCameraDistance;

    // Modelo y animaciones del asesino.
    private GameObject assassinVisual;
    private Animator assassinAnimator;
    private bool assassinVisualConfigured;
    private Vector3 lastAnimationPosition;

    // Velocidad horizontal actual.
    private Vector3 currentHorizontalVelocity;

    // Buffer para la colisión de cámara.
    private readonly RaycastHit[] cameraHits = new RaycastHit[16];

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Configuramos el modelo en cada cliente.
        ConfigureAssassinVisual();

        // Este jugador pertenece a otro cliente.
        if (!photonView.IsMine)
        {
            DisableRemotePlayerCamera();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        DetermineRole();
        SetupCamera();
    }

    private void LateUpdate()
    {
        // Reintentamos si el rol todavía no estaba asignado.
        if (!assassinVisualConfigured)
        {
            ConfigureAssassinVisual();
        }

        UpdateAssassinAnimation();
    }

    // =========================================================
    // MODELO Y ANIMACIONES DEL ASESINO
    // =========================================================

    private void ConfigureAssassinVisual()
    {
        if (assassinVisualConfigured || photonView.Owner == null)
            return;

        int assassinActor = RoleManager.GetKillerActorNumber();

        // El rol puede no estar asignado todavía.
        if (assassinActor <= 0)
            return;

        // Solo cambiamos la apariencia del asesino.
        if (photonView.Owner.ActorNumber != assassinActor)
        {
            assassinVisualConfigured = true;
            return;
        }

        // Cargamos el modelo desde Assets/Resources.
        GameObject assassinPrefab =
            Resources.Load<GameObject>(assassinModelResource);

        if (assassinPrefab == null)
        {
            Debug.LogError(
                "[PlayerMovement] No se encontró el modelo del asesino en " +
                "Assets/Resources/" + assassinModelResource + ".fbx"
            );

            assassinVisualConfigured = true;
            return;
        }

        // Ocultamos la malla original del jugador asesino.
        // No modificamos el collider ni el objeto Player.
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

        // Instanciamos Gero como hijo del jugador.
        assassinVisual = Instantiate(
            assassinPrefab,
            transform,
            false
        );

        assassinVisual.name = "futuroGero";

        // Posición inicial del modelo.
        // La alineación definitiva se realiza después de iniciar
        // el Animator, para medir la pose inicial del esqueleto.
        assassinVisual.transform.localPosition = Vector3.zero;
        assassinVisual.transform.localRotation = Quaternion.identity;
        assassinVisual.transform.localScale = Vector3.one;

        // -----------------------------------------------------
        // ANIMATOR
        // -----------------------------------------------------

        assassinAnimator =
            assassinVisual.GetComponentInChildren<Animator>(true);

        RuntimeAnimatorController geroController =
            Resources.Load<RuntimeAnimatorController>(
                assassinAnimatorControllerResource
            );

        if (assassinAnimator == null)
        {
            Debug.LogError(
                "[PlayerMovement] El modelo futurojero no tiene Animator. " +
                "Revisá el Rig del FBX."
            );
        }
        else
        {
            if (geroController != null)
            {
                assassinAnimator.runtimeAnimatorController =
                    geroController;
            }
            else
            {
                Debug.LogError(
                    "[PlayerMovement] No se encontró el Animator Controller " +
                    assassinAnimatorControllerResource +
                    " dentro de Resources."
                );
            }

            // La posición del jugador la controla CharacterController,
            // no el movimiento de raíz de las animaciones.
            assassinAnimator.applyRootMotion = false;

            // Mantiene las animaciones de los jugadores remotos actualizadas.
            assassinAnimator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;

            // El Animator debe permanecer activado.
            assassinAnimator.enabled = true;

            if (geroController != null)
            {
                assassinAnimator.Rebind();
                assassinAnimator.Update(0f);
            }
        }

        // -----------------------------------------------------
        // ALINEACIÓN VISUAL DE GERO
        // -----------------------------------------------------

        // Importante: se realiza después de asignar e inicializar
        // el Animator, y no mueve el objeto Player ni su collider.
        AlignAssassinVisualToPlayerBase();

        // Guardamos los renderizadores para poder ocultar el modelo
        // únicamente en la vista local del asesino.
        Renderer[] modelRenderers =
            assassinVisual.GetComponentsInChildren<Renderer>(true);

        Debug.Log(
            $"[Gero] Visual creado. Player={transform.position}; " +
            $"posición local={assassinVisual.transform.localPosition}; " +
            $"Animator={(assassinAnimator != null ? assassinAnimator.name : "NO ENCONTRADO")}; " +
            $"Controller={(assassinAnimator != null && assassinAnimator.runtimeAnimatorController != null ? assassinAnimator.runtimeAnimatorController.name : "NO ASIGNADO")}; " +
            $"Avatar válido={(assassinAnimator != null && assassinAnimator.avatar != null && assassinAnimator.avatar.isValid)}"
        );

        // El asesino juega en primera persona.
        // Los otros clientes sí deben ver su modelo.
        if (photonView.IsMine)
        {
            foreach (Renderer modelRenderer in modelRenderers)
            {
                if (modelRenderer != null)
                    modelRenderer.enabled = false;
            }
        }

        lastAnimationPosition = transform.position;
        assassinVisualConfigured = true;
    }

    // Alinea la parte inferior visual de Gero con la base del
    // CharacterController. Solo desplaza el objeto visual.
    private void AlignAssassinVisualToPlayerBase()
    {
        if (assassinVisual == null || controller == null)
            return;

        Renderer[] renderers =
            assassinVisual.GetComponentsInChildren<Renderer>(true);

        bool foundRenderer = false;
        Bounds modelBounds = new Bounds();

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer == null)
                continue;

            if (!foundRenderer)
            {
                modelBounds = modelRenderer.bounds;
                foundRenderer = true;
            }
            else
            {
                modelBounds.Encapsulate(modelRenderer.bounds);
            }
        }

        if (!foundRenderer)
        {
            Debug.LogWarning(
                "[Gero] No se encontraron renderizadores para alinear el modelo."
            );
            return;
        }

        // Base física del jugador.
        float floorHeight = controller.bounds.min.y;

        // Parte inferior visible del modelo en la pose inicial.
        float modelBottom = modelBounds.min.y;

        // Diferencia necesaria para apoyar el modelo en la base.
        float verticalOffset = floorHeight - modelBottom;

        // Movemos el modelo visual, nunca el Player.
        assassinVisual.transform.position +=
            Vector3.up * verticalOffset;

        Debug.Log(
            $"[Gero] Alineación vertical: {verticalOffset:F3} m. " +
            $"Base Player={floorHeight:F3}; " +
            $"parte inferior Gero={modelBottom:F3}."
        );
    }

    private void UpdateAssassinAnimation()
    {
        if (assassinVisual == null ||
            assassinAnimator == null ||
            !assassinAnimator.isActiveAndEnabled ||
            assassinAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.001f);

        // Calculamos el desplazamiento real del jugador.
        Vector3 worldVelocity =
            (transform.position - lastAnimationPosition) / deltaTime;

        // Convertimos la velocidad a los ejes locales del jugador.
        Vector3 localVelocity =
            transform.InverseTransformDirection(worldVelocity);

        float referenceSpeed =
            Mathf.Max(animationReferenceSpeed, 0.01f);

        float moveX = Mathf.Clamp(
            localVelocity.x / referenceSpeed,
            -1f,
            1f
        );

        float moveY = Mathf.Clamp(
            localVelocity.z / referenceSpeed,
            -1f,
            1f
        );

        // Evitamos pequeñas vibraciones estando quieto.
        if (new Vector2(moveX, moveY).magnitude < 0.03f)
        {
            moveX = 0f;
            moveY = 0f;
        }

        // Deben coincidir con los parámetros del Blend Tree.
        assassinAnimator.SetFloat(
            "MoveX",
            moveX,
            animationSmoothTime,
            deltaTime
        );

        assassinAnimator.SetFloat(
            "MoveY",
            moveY,
            animationSmoothTime,
            deltaTime
        );

        lastAnimationPosition = transform.position;
    }

    // =========================================================
    // UPDATE PRINCIPAL
    // =========================================================

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        // Q activa o desactiva el bloqueo del movimiento.
        if (Keyboard.current != null &&
            Keyboard.current.qKey.wasPressedThisFrame &&
            !ConfirmarSalida.MenuAbierto &&
            !FinDePartida.Terminada)
        {
            movementLocked = !movementLocked;

            if (movementLocked)
            {
                cachedMoveInput = Vector2.zero;
                currentHorizontalVelocity = Vector3.zero;

                isHoldingJump = false;
                jumpBufferCounter = 0f;
            }

            Debug.Log(
                movementLocked
                    ? "Movimiento bloqueado."
                    : "Movimiento habilitado."
            );
        }

        // Si hay un menú abierto o terminó la partida,
        // detenemos el desplazamiento horizontal y conservamos la gravedad.
        if (ConfirmarSalida.MenuAbierto || FinDePartida.Terminada)
        {
            cachedMoveInput = Vector2.zero;
            currentHorizontalVelocity = Vector3.zero;
            isHoldingJump = false;
            jumpBufferCounter = 0f;

            HandleJump();
            HandleMovement();

            return;
        }

        if (movementLocked)
        {
            cachedMoveInput = Vector2.zero;
            currentHorizontalVelocity = Vector3.zero;
            isHoldingJump = false;
            jumpBufferCounter = 0f;
        }
        else
        {
            ReadInput();
        }

        // La cámara sigue funcionando aunque el movimiento esté bloqueado.
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

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation = thirdPersonAngle;

        UpdateEscapistCameraPosition(false);
    }

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
            float top = bounds.max.y - transform.position.y;

            pivotHeight = Mathf.Clamp(
                top * heightOverProp,
                minPivotHeight,
                thirdPersonHeight
            );

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

            radius = Mathf.Min(
                cameraCollisionRadius,
                Mathf.Max(0.05f, top * 0.25f)
            );
        }

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

            // Ignoramos los colliders del propio jugador.
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

        transform.Rotate(Vector3.up * mouseX);

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

        // No permitimos saltar con el movimiento bloqueado.
        if (!movementLocked &&
            jumpBufferCounter > 0f &&
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