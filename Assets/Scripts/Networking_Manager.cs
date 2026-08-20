using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
 
/// <summary>
/// Gestiona toda la conexión con Photon:
/// conectar al servidor, crear sala con código,
/// unirse por código y notificar a la UI.
/// Vive en Lobby.unity y persiste entre escenas.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }
 
    [Header("Configuración")]
    [Tooltip("Máximo de jugadores por sala.")]
    public byte maxPlayersPerRoom = 2;
 
    // Evento para que la UI del Lobby se entere de errores al unirse
    public static event System.Action<string> OnJoinRoomFailedCustom;
 
    // Evento para que la UI sepa que ya estamos conectados al Master Server
    public static event System.Action OnConnectedToServer;
 
    private string _pendingRoomCodeToJoin;
    private bool _isCreatingRoom;
 
 
    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------
 
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
 
        Instance = this;
 
        // No destruir este objeto al cambiar de escena
        DontDestroyOnLoad(gameObject);
 
        // Photon sincroniza automáticamente el cambio de escena
        // entre todos los jugadores de la sala.
        PhotonNetwork.AutomaticallySyncScene = true;
    }
 
 
    private void Start()
    {
        // Suscribirse a los eventos del LobbyUIManager
        LobbyUIManager.CreateRoomRequested += HandleCreateRoomRequested;
        LobbyUIManager.JoinRoomRequested += HandleJoinRoomRequested;
 
        Connect();
    }
 
 
    private void OnDestroy()
    {
        LobbyUIManager.CreateRoomRequested -= HandleCreateRoomRequested;
        LobbyUIManager.JoinRoomRequested -= HandleJoinRoomRequested;
    }
 
 
    // ---------------------------------------------------------
    // CONEXIÓN A PHOTON
    // ---------------------------------------------------------
 
    private void Connect()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.GameVersion = "1.0";
 
            PhotonNetwork.ConnectUsingSettings();
 
            Debug.Log("[Photon] Conectando al Master Server...");
        }
    }
 
 
    public override void OnConnectedToMaster()
    {
        Debug.Log("[Photon] Conectado al Master Server.");
 
        OnConnectedToServer?.Invoke();
    }
 
 
    // ---------------------------------------------------------
    // CREAR SALA
    // ---------------------------------------------------------
 
    private void HandleCreateRoomRequested()
    {
        _isCreatingRoom = true;
 
        CreateRoomWithGeneratedCode();
    }
 
 
    /// <summary>
    /// Genera un código corto y crea una sala usando ese código
    /// como nombre de sala.
    /// </summary>
    public void CreateRoomWithGeneratedCode()
    {
        string code = GenerateRoomCode(6);
 
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
 
            // La sala no aparece en listados públicos.
            // Solo se puede entrar usando el código.
            IsVisible = false,
 
            IsOpen = true
        };
 
        PhotonNetwork.CreateRoom(code, options);
 
        Debug.Log($"[Photon] Creando sala con código: {code}");
    }
 
 
    private string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
 
        var sb = new System.Text.StringBuilder();
 
        var rng = new System.Random();
 
        for (int i = 0; i < length; i++)
        {
            sb.Append(chars[rng.Next(chars.Length)]);
        }
 
        return sb.ToString();
    }
 
 
    // ---------------------------------------------------------
    // UNIRSE A SALA
    // ---------------------------------------------------------
 
    private void HandleJoinRoomRequested()
    {
        // El botón "UNIRSE" solamente abre el panel
        // donde el jugador introduce el código.
        //
        // La unión real se realiza desde JoinRoomByCode().
    }
 
 
    /// <summary>
    /// Se llama desde el panel de "Unirse a sala"
    /// utilizando el código introducido por el jugador.
    /// </summary>
    public void JoinRoomByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            OnJoinRoomFailedCustom?.Invoke("Ingresá un código de sala.");
 
            return;
        }
 
        // Elimina espacios y convierte el código a mayúsculas.
        _pendingRoomCodeToJoin = code.Trim().ToUpper();
 
        Debug.Log($"[Photon] Intentando entrar a sala: {_pendingRoomCodeToJoin}");
 
        PhotonNetwork.JoinRoom(_pendingRoomCodeToJoin);
    }
 
 
    /// <summary>
    /// Callback de Photon cuando no se puede entrar a una sala.
    /// IMPORTANTE: este nombre NO se debe cambiar porque
    /// pertenece al sistema de callbacks de Photon.
    /// </summary>
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning(
            $"[Photon] Error al unirse a sala '{_pendingRoomCodeToJoin}': {message}"
        );
 
        OnJoinRoomFailedCustom?.Invoke(
            "No se encontró la sala. Verificá el código."
        );
    }
 
 
    // ---------------------------------------------------------
    // ERROR AL CREAR SALA
    // ---------------------------------------------------------
 
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning(
            $"[Photon] Error al crear sala: {message}"
        );
 
        _isCreatingRoom = false;
 
        // Si el código generado ya existía,
        // genera otro código e intenta nuevamente.
        CreateRoomWithGeneratedCode();
    }
 
 
    // ---------------------------------------------------------
    // ENTRAR A LA SALA
    // ---------------------------------------------------------
 
    public override void OnJoinedRoom()
    {
        Debug.Log(
            $"[Photon] Entramos a la sala: " +
            $"{PhotonNetwork.CurrentRoom.Name}, " +
            $"jugadores: " +
            $"{PhotonNetwork.CurrentRoom.PlayerCount}/" +
            $"{PhotonNetwork.CurrentRoom.MaxPlayers}"
        );
 
        _isCreatingRoom = false;
 
        // Cargar el lobby de espera.
        // Como AutomaticallySyncScene = true,
        // todos los jugadores cargarán la misma escena.
        PhotonNetwork.LoadLevel("RoomLobby");
    }
 
 
    // ---------------------------------------------------------
    // JUGADORES
    // ---------------------------------------------------------
 
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(
            $"[Photon] Se unió: {newPlayer.NickName} " +
            $"({PhotonNetwork.CurrentRoom.PlayerCount}/" +
            $"{PhotonNetwork.CurrentRoom.MaxPlayers})"
        );
    }
 
 
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log(
            $"[Photon] Salió: {otherPlayer.NickName}"
        );
    }
 
 
    // ---------------------------------------------------------
    // INICIAR PARTIDA
    // ---------------------------------------------------------
 
    /// <summary>
    /// Solo el MasterClient puede iniciar la partida.
    /// </summary>
    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }
 
        // Cierra la sala para que no entren nuevos jugadores.
        PhotonNetwork.CurrentRoom.IsOpen = false;
 
        // Carga la escena del juego.
        // Photon la sincronizará con todos los jugadores.
        PhotonNetwork.LoadLevel("Game");
    }
 
 
    // ---------------------------------------------------------
    // OBTENER CÓDIGO DE SALA
    // ---------------------------------------------------------
 
    public string GetCurrentRoomCode()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            return PhotonNetwork.CurrentRoom.Name;
        }
 
        return "";
    }
}
 