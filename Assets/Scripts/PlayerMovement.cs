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

    [Header("Modelo y animaciones del asesino")]
    [SerializeField] private string assassinModelResource = "futurojero";
    [SerializeField] private string assassinAnimatorControllerResource = "GeroController";
    [SerializeField] private float animationReferenceSpeed = 5f;
    [SerializeField] private float animationSmoothTime = 0.1f;

    [Header("Ajuste visual de Gero")]
    [Tooltip("Desplaza solo el modelo visual en vertical. Usá un valor negativo si queda flotando.")]
    [SerializeField] private float assassinVisualYOffset = 0f;

    private CharacterController controller;

    private float verticalRotation;
    private Vector3 velocity;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private bool isHoldingJump;
    private Vector2 cachedMoveInput;

    private bool isHunter;

    // Q activa o desactiva el bloqueo del movimiento.
    private bool movementLocked = false;

    private float currentCameraDistance;

    // Modelo y animaciones del asesino.
    private GameObject assassinVisual;
    private Animator assassinAnimator;
    private bool assassinVisualConfigured;
    private Vector3 lastAnimationPosition;

    // Velocidad horizontal actual.
    private Vector3 currentHorizontalVelocity;

    // Buffer reutilizable para el SphereCast de la cámara.
    private readonly RaycastHit[] cameraHits = new RaycastHit[16];

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Configuramos el modelo en cada cliente para que todos
        // puedan ver al asesino.
        ConfigureAssassinVisual();

        // Este Player pertenece a otro cliente.
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
        // Si el rol todavía no se había asignado al iniciar,
        // volvemos a comprobarlo.
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

        // Puede que el rol todavía no se haya asignado.
        if (assassinActor <= 0)
            return;

        // Solo cambiamos la apariencia del asesino.
        // Los buscados conservan sus modelos y transformaciones.
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

        // Ocultamos la malla de la cápsula original
        // únicamente para el asesino.
        // No desactivamos ni modificamos sus colliders.
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

        // Posición manual del modelo visual. No usamos Renderer.bounds:
        // sus límites pueden variar con la pose de la malla animada.
        // Ajustá assassinVisualYOffset desde el Inspector si Gero flota o se hunde.
        assassinVisual.transform.localPosition =
            new Vector3(0f, assassinVisualYOffset, 0f);
        assassinVisual.transform.localRotation = Quaternion.identity;
        assassinVisual.transform.localScale = Vector3.one;

        // -----------------------------------------------------
        // ANIMATOR
        // -----------------------------------------------------

        // Buscamos el Animator del modelo.
        assassinAnimator =
            assassinVisual.GetComponentInChildren<Animator>(true);

        // Cargamos el controlador desde Resources.
        RuntimeAnimatorController geroController =
            Resources.Load<RuntimeAnimatorController>(
                assassinAnimatorControllerResource
            );

        if (assassinAnimator == null)
        {
            Debug.LogError(
                "[PlayerMovement] El modelo futurojero no tiene un Animator. " +
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

            // El movimiento lo controla el Player,
            // no la animación. Animamos también cuando el modelo remoto
            // está fuera de la cámara para que el estado no quede congelado.
            assassinAnimator.applyRootMotion = false;
            assassinAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            assassinAnimator.enabled = true;

            // Reiniciamos el rig con el controlador ya asignado.
            // IMPORTANTE: en GeroController, Base Layer está vacía y
            // New Layer contiene el Blend Tree con peso predeterminado 0.
            // Por eso forzamos su peso a 1 DESPUÉS de Rebind().
            if (geroController != null)
            {
                assassinAnimator.Rebind();

                int animationLayer =
                    assassinAnimator.GetLayerIndex("New Layer");

                if (animationLayer >= 0)
                {
                    assassinAnimator.SetLayerWeight(animationLayer, 1f);

                    Debug.Log(
                        $"[Gero] Capa '{assassinAnimator.GetLayerName(animationLayer)}' " +
                        $"activada con peso {assassinAnimator.GetLayerWeight(animationLayer):F1}."
                    );
                }
                else
                {
                    Debug.LogError(
                        "[Gero] No se encontró la capa 'New Layer' en GeroController. " +
                        "Revisá el nombre exacto en la pestaña Layers del Animator."
                    );
                }

                // El Blend Tree comienza quieto. Luego LateUpdate actualizará
                // MoveX y MoveY usando el movimiento real del Player.
                assassinAnimator.SetFloat("MoveX", 0f);
                assassinAnimator.SetFloat("MoveY", 0f);
                assassinAnimator.Update(0f);
            }
        }

        // Conservamos los renderers para ocultar el modelo en la vista
        // local de primera persona.
        Renderer[] modelRenderers =
            assassinVisual.GetComponentsInChildren<Renderer>(true);

        Debug.Log(
            $"[Gero] Visual creado. Player={transform.position}; " +
            $"offset local={assassinVisual.transform.localPosition}; " +
            $"Animator={(assassinAnimator != null ? assassinAnimator.name : "NO ENCONTRADO")}; " +
            $"Controller={(assassinAnimator != null && assassinAnimator.runtimeAnimatorController != null ? assassinAnimator.runtimeAnimatorController.name : "NO ASIGNADO")}; " +
            $"Avatar válido={(assassinAnimator != null && assassinAnimator.avatar != null && assassinAnimator.avatar.isValid)}"
        );

        // El asesino juega en primera persona.
        // Ocultamos su modelo únicamente en su propia pantalla.
        // Los demás jugadores sí podrán verlo.
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

    private void UpdateAssassinAnimation()
    {
        if (assassinVisual == null || assassinAnimator == null ||
            !assassinAnimator.isActiveAndEnabled ||
            assassinAnimator.runtimeAnimatorController == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.001f);

        // Calculamos el desplazamiento real del jugador.
        Vector3 worldVelocity =
            (transform.position - lastAnimationPosition) / deltaTime;

        // Convertimos el movimiento a los ejes locales del jugador.
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

        // Evitamos pequeñas vibraciones cuando está quieto.
        if (new Vector2(moveX, moveY).magnitude < 0.03f)
        {
            moveX = 0f;
            moveY = 0f;
        }

        // Estos nombres deben coincidir con los parámetros
        // del Blend Tree de GeroController.
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
        // La cámara y la rotación siguen funcionando.
        if (Keyboard.current != null &&
            Keyboard.current.qKey.wasPressedThisFrame &&
            !ConfirmarSalida.MenuAbierto &&
            !FinDePartida.Terminada)
        {
            movementLocked = !movementLocked;

            if (movementLocked)
            {
                // Frenamos inmediatamente el desplazamiento horizontal.
                cachedMoveInput = Vector2.zero;
                currentHorizontalVelocity = Vector3.zero;

                // Cancelamos saltos pendientes.
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
        // detenemos el movimiento y conservamos la gravedad.
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
            // No caminar ni saltar mientras Q esté activada.
            cachedMoveInput = Vector2.zero;
            currentHorizontalVelocity = Vector3.zero;
            isHoldingJump = false;
            jumpBufferCounter = 0f;
        }
        else
        {
            ReadInput();
        }

        // La cámara y la rotación continúan funcionando
        // independientemente del bloqueo del movimiento.
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

        // Primera persona: altura de los ojos.
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

        // La rotación horizontal sigue funcionando aunque
        // movementLocked esté activado.
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

        // Colisión de cámara.
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

        // Si Q está activada, el movimiento horizontal
        // ya está en cero. La gravedad sigue funcionando.
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

        // No permitimos iniciar un salto con el movimiento bloqueado.
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