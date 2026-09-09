using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviourPun
{
    [Header("Movimiento")]
    [SerializeField] private float speed = 5f;

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

    [Tooltip("Ángulo vertical de la cámara.")]
    [SerializeField] private float thirdPersonAngle = 12f;

    [Tooltip("Cuánto espacio extra dejamos alrededor del prop.")]
    [SerializeField] private float propCameraPadding = 1.25f;

    [Tooltip("Qué tan rápido se aleja/acerca la cámara.")]
    [SerializeField] private float cameraDistanceSmooth = 5f;

    [Header("Primera persona")]
    [SerializeField] private float mouseSensitivity = 0.1f;

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

    private void Awake()
    {
        controller =
            GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (!photonView.IsMine)
        {
            DisableRemotePlayerCamera();
            enabled = false;
            return;
        }

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;

        DetermineRole();

        SetupCamera();
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

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

    private void DetermineRole()
    {
        if (!RoleManager.HasKiller)
        {
            isHunter = false;
            return;
        }

        isHunter =
            GameManager.IsLocalAssassin();
    }

    private void SetupCamera()
    {
        if (playerCamera == null &&
            cameraHolder != null)
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
            Vector3.zero;

        cameraHolder.localRotation =
            Quaternion.identity;

        playerCamera.transform.localPosition =
            Vector3.zero;

        playerCamera.transform.localRotation =
            Quaternion.identity;

        verticalRotation = 0f;
    }

    private void SetupEscapistCamera()
    {
        if (cameraHolder == null)
            return;

        currentCameraDistance =
            thirdPersonDistance;

        UpdateEscapistCameraPosition(true);
    }

    private void HandleEscapistCamera()
    {
        if (cameraHolder == null)
            return;

        UpdateEscapistCameraPosition(false);

        // La cámara NO gira con el mouse.
        cameraHolder.localRotation =
            Quaternion.Euler(
                thirdPersonAngle,
                0f,
                0f
            );
    }

    private void UpdateEscapistCameraPosition(
        bool instant)
    {
        float targetDistance =
            CalculateCameraDistance();

        if (instant)
        {
            currentCameraDistance =
                targetDistance;
        }
        else
        {
            currentCameraDistance =
                Mathf.Lerp(
                    currentCameraDistance,
                    targetDistance,
                    cameraDistanceSmooth *
                    Time.deltaTime
                );
        }

        cameraHolder.localPosition =
            new Vector3(
                0f,
                thirdPersonHeight,
                -currentCameraDistance
            );

        cameraHolder.localRotation =
            Quaternion.Euler(
                thirdPersonAngle,
                0f,
                0f
            );

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition =
                Vector3.zero;

            playerCamera.transform.localRotation =
                Quaternion.identity;
        }
    }

    private float CalculateCameraDistance()
    {
        float targetDistance =
            thirdPersonDistance;

        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        if (renderers == null ||
            renderers.Length == 0)
        {
            return targetDistance;
        }

        Bounds combinedBounds =
            new Bounds(
                transform.position,
                Vector3.zero
            );

        bool foundRenderer = false;

        foreach (Renderer renderer in renderers)
        {
            // No contamos UI ni partículas raras.
            if (renderer == null)
                continue;

            if (!renderer.enabled)
                continue;

            // No usamos la propia cámara.
            if (renderer.GetComponent<Camera>() != null)
                continue;

            if (!foundRenderer)
            {
                combinedBounds =
                    renderer.bounds;

                foundRenderer = true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    renderer.bounds
                );
            }
        }

        if (!foundRenderer)
            return targetDistance;

        float largestExtent =
            Mathf.Max(
                combinedBounds.extents.x,
                combinedBounds.extents.y,
                combinedBounds.extents.z
            );

        // El prop conserva algo de aire alrededor
        // para que la cámara no quede pegada.
        float requiredDistance =
            largestExtent *
            propCameraPadding;

        targetDistance =
            Mathf.Max(
                thirdPersonDistance,
                requiredDistance
            );

        targetDistance =
            Mathf.Clamp(
                targetDistance,
                minimumCameraDistance,
                maximumCameraDistance
            );

        return targetDistance;
    }

    private void HandleHunterLook()
    {
        if (cameraHolder == null)
            return;

        Vector2 lookInput =
            Vector2.zero;

        if (Mouse.current != null)
        {
            lookInput =
                Mouse.current.delta.ReadValue();
        }

        float mouseX =
            lookInput.x *
            mouseSensitivity;

        float mouseY =
            lookInput.y *
            mouseSensitivity;

        transform.Rotate(
            Vector3.up * mouseX
        );

        verticalRotation -=
            mouseY;

        verticalRotation =
            Mathf.Clamp(
                verticalRotation,
                -80f,
                80f
            );

        cameraHolder.localRotation =
            Quaternion.Euler(
                verticalRotation,
                0f,
                0f
            );
    }

    private void ReadInput()
    {
        cachedMoveInput =
            Vector2.zero;

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

    private void HandleMovement()
    {
        Vector3 move =
            transform.right *
            cachedMoveInput.x;

        move +=
            transform.forward *
            cachedMoveInput.y;

        move *= speed;

        Vector3 finalMove =
            move + velocity;

        controller.Move(
            finalMove *
            Time.deltaTime
        );
    }

    private void DisableRemotePlayerCamera()
    {
        if (playerCamera == null &&
            cameraHolder != null)
        {
            playerCamera =
                cameraHolder.GetComponentInChildren<Camera>();
        }

        if (playerCamera != null)
            playerCamera.enabled = false;

        AudioListener listener =
            GetComponentInChildren<AudioListener>();

        if (listener != null)
            listener.enabled = false;
    }
}