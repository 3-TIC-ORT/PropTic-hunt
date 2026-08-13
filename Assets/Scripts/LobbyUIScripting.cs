using UnityEngine;
using UnityEngine.Events;
 
/// <summary>
/// Controla la lógica de la interfaz del lobby principal.
///
/// Los métodos OnCreateRoomButtonPressed() / OnJoinRoomButtonPressed() quedan
/// preparados como puntos de enganche para conectar el futuro sistema multiplayer,
/// sin necesidad de tocar la UI ni los botones más adelante.
///
/// Se exponen DOS formas de suscribirse, para máxima flexibilidad:
///   1) UnityEvent (onCreateRoomRequested / onJoinRoomRequested): enganchable desde el
///      Inspector, ideal para prototipos o para conectar varios listeners visualmente.
///   2) Eventos estáticos de C# (CreateRoomRequested / JoinRoomRequested): ideal para que
///      un futuro NetworkManager (Mirror, Fishnet, Netcode, Photon, etc.) se suscriba
///      por código sin necesitar una referencia en el Inspector.
///
/// Colocar este componente en un GameObject vacío llamado "LobbyManager".
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    [Header("Eventos - Conectar futura lógica multiplayer aquí (opcional, vía Inspector)")]
    [Tooltip("Se dispara al presionar CREAR SALA.")]
    public UnityEvent onCreateRoomRequested;
 
    [Tooltip("Se dispara al presionar UNIRSE A UNA SALA.")]
    public UnityEvent onJoinRoomRequested;
 
    // Alternativa por código: cualquier sistema futuro puede hacer
    // LobbyUIManager.CreateRoomRequested += MiMetodo; en su propio OnEnable().
    public static event System.Action CreateRoomRequested;
    public static event System.Action JoinRoomRequested;
 
    /// <summary>
    /// Enganchar este método al evento OnClick() del botón "CREAR SALA" en el Inspector.
    /// </summary>
    public void OnCreateRoomButtonPressed()
    {
        Debug.Log("[Lobby] CREAR SALA presionado. Listo para conectar creación de partida.");
 
        // TODO (futuro): reemplazar/complementar con la llamada real, por ejemplo:
        // NetworkManager.Instance.CreateRoom(roomSettings);
        // UIManager.Instance.ShowRoomConfigScreen();
 
        onCreateRoomRequested?.Invoke();
        CreateRoomRequested?.Invoke();
    }
 
    /// <summary>
    /// Enganchar este método al evento OnClick() del botón "UNIRSE A UNA SALA" en el Inspector.
    /// </summary>
    public void OnJoinRoomButtonPressed()
    {
        Debug.Log("[Lobby] UNIRSE A UNA SALA presionado. Listo para conectar búsqueda/unión.");
 
        // TODO (futuro): reemplazar/complementar con la llamada real, por ejemplo:
        // NetworkManager.Instance.RequestRoomList();
        // UIManager.Instance.ShowRoomListScreen();
 
        onJoinRoomRequested?.Invoke();
        JoinRoomRequested?.Invoke();
    }
}
 
