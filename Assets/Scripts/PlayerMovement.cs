using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float mouseSensitivity = 0.1f;
    public Transform cameraHolder;

    [Header("Salto y gravedad")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float fallMultiplier = 2.5f;   // qué tan más rápido cae que sube
    public float lowJumpMultiplier = 2f;  // si soltás el espacio antes, corta el salto

    [Header("Ayudas de control (feel)")]
    public float coyoteTime = 0.15f;      // margen para saltar tras dejar el borde
    public float jumpBufferTime = 0.15f;  // margen para que el salto "se guarde" antes de tocar piso

    private CharacterController controller;
    private float verticalRotation = 0f;
    private Vector3 velocity;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool isHoldingJump;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
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

        // --- Movimiento horizontal ---
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * speed * Time.deltaTime);

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
            // Cayendo: gravedad más fuerte
            velocity.y += gravity * fallMultiplier * Time.deltaTime;
        }
        else if (velocity.y > 0 && !isHoldingJump)
        {
            // Soltó el botón antes de tiempo: corta el salto (salto más bajo)
            velocity.y += gravity * lowJumpMultiplier * Time.deltaTime;
        }
        else
        {
            // Subida normal
            velocity.y += gravity * Time.deltaTime;
        }

        // Evitar que la velocidad negativa se acumule infinito estando en el piso
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        controller.Move(velocity * Time.deltaTime);
    }
}