using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ConfirmarSalida : MonoBehaviourPunCallbacks
{
    // Los demás scripts consultan esto para ignorar el input.
    public static bool MenuAbierto { get; private set; }

    [SerializeField] private GameObject panelConfirmacion;
    [SerializeField] private string escenaAlSalir = "Lobby";

    private void Awake()
    {
        MenuAbierto = false;
    }

    private void Start()
    {
        panelConfirmacion.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (MenuAbierto)
                CerrarPanel();
            else
                AbrirPanel();
        }
    }

    // Botón rojo.
    public void AbrirPanel()
    {
        MenuAbierto = true;
        panelConfirmacion.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Botón "No".
    public void CerrarPanel()
    {
        MenuAbierto = false;
        panelConfirmacion.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Botón "Si".
    public void ConfirmarSalir()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            SceneManager.LoadScene(escenaAlSalir);
    }

    public override void OnLeftRoom()
    {
        MenuAbierto = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(escenaAlSalir);
    }
}