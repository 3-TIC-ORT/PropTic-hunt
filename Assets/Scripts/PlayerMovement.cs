using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PhotonView))]
public class PlayerMovement : MonoBehaviourPun
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float mouseSensitivity = 0.1f;
    public Transform cameraHolder;

    [Header("Salto y gravedad")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Ayudas de control (feel)")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    private CharacterController controller;
    private float verticalRotation = 0f;
    private Vector3 velocity;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool isHoldingJump;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Si este objeto NO me pertenece (es el jugador remoto),
        // apago cámara y audio para no pisar al jugador local,
        // y no proceso su input.
        if (!photonView.IsMine)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = false;

            AudioListener listener = cameraHolder.GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;

            enabled = false; // apaga este script entero para el remoto
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Por las dudas (doble seguro si enabled=false no alcanzó a tiempo)
        if (!photonView.IsMine) return;

        // --- Lectura de inputs ---
        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;
        bool jumpPressedThisFrame = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;

            jumpPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame;
            isHoldingJump = Keyboard.current.spaceKey.isPressed;
        }

        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue();
        }

        // --- Rotación con el mouse ---
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        // --- Coyote time: cuenta regresiva desde que dejás el piso ---
        if (controller.isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        // --- Jump buffer: guarda el input de salto un ratito ---
        if (jumpPressedThisFrame)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        // --- Ejecutar salto si hay buffer Y coyote time disponibles ---
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // --- Gravedad variable (caída realista) ---
        if (velocity.y < 0)
        {
            velocity.y += gravity * fallMultiplier * Time.deltaTime;
        }
        else if (velocity.y > 0 && !isHoldingJump)
        {
            velocity.y += gravity * lowJumpMultiplier * Time.deltaTime;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // --- Movimiento horizontal ---
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        move *= speed;

        // --- Combinamos horizontal + vertical en UN SOLO Move() ---
        Vector3 finalMove = move + velocity;
        controller.Move(finalMove * Time.deltaTime);
    }
}