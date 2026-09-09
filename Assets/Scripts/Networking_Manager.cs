using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    // La sala permite entre 1 y 6 jugadores.
    // Para empezar una partida necesitamos al menos 2.
    public const byte MaxPlayersPerRoom = 6;
    public const int MinPlayersToStart = 2;

    public static event System.Action<string> OnJoinRoomFailedCustom;
    public static event System.Action OnConnectedToServer;

    private string _pendingRoomCodeToJoin;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Start()
    {
        LobbyUIManager.CreateRoomRequested += HandleCreateRoomRequested;
        LobbyUIManager.JoinRoomRequested += HandleJoinRoomRequested;

        Connect();
    }

    private void OnDestroy()
    {
        LobbyUIManager.CreateRoomRequested -= HandleCreateRoomRequested;
        LobbyUIManager.JoinRoomRequested -= HandleJoinRoomRequested;
    }

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

    private void HandleCreateRoomRequested()
    {
        CreateRoomWithGeneratedCode();
    }

    public void CreateRoomWithGeneratedCode()
    {
        string code = GenerateRoomCode(6);

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = MaxPlayersPerRoom,
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

    private void HandleJoinRoomRequested()
    {
        // El panel de unión se encarga de pedir el código.
    }

    public void JoinRoomByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            OnJoinRoomFailedCustom?.Invoke(
                "Ingresá un código de sala."
            );

            return;
        }

        _pendingRoomCodeToJoin = code.Trim().ToUpper();

        Debug.Log(
            $"[Photon] Intentando entrar a sala: " +
            $"{_pendingRoomCodeToJoin}"
        );

        PhotonNetwork.JoinRoom(_pendingRoomCodeToJoin);
    }

    public override void OnJoinRoomFailed(
        short returnCode,
        string message)
    {
        Debug.LogWarning(
            $"[Photon] Error al unirse a sala " +
            $"'{_pendingRoomCodeToJoin}': {message}"
        );

        OnJoinRoomFailedCustom?.Invoke(
            "No se encontró la sala o está llena. Verificá el código."
        );
    }

    public override void OnCreateRoomFailed(
        short returnCode,
        string message)
    {
        Debug.LogWarning(
            $"[Photon] Error al crear sala: {message}"
        );

        CreateRoomWithGeneratedCode();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log(
            $"[Photon] Entramos a la sala: " +
            $"{PhotonNetwork.CurrentRoom.Name}, " +
            $"jugadores: " +
            $"{PhotonNetwork.CurrentRoom.PlayerCount}/" +
            $"{PhotonNetwork.CurrentRoom.MaxPlayers}"
        );

        PhotonNetwork.LoadLevel("RoomLobby");
    }

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

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null)
            return;

        int playerCount =
            PhotonNetwork.CurrentRoom.PlayerCount;

        if (playerCount < MinPlayersToStart)
        {
            Debug.LogWarning(
                $"[Photon] No se puede iniciar la partida: " +
                $"se necesitan al menos " +
                $"{MinPlayersToStart} jugadores."
            );

            return;
        }

        if (playerCount > MaxPlayersPerRoom)
        {
            Debug.LogWarning(
                "[Photon] Hay demasiados jugadores."
            );

            return;
        }

        if (!RoleManager.HasKiller)
        {
            Debug.LogWarning(
                "[Photon] No se puede iniciar la partida: " +
                "todavía no existe un Hunter."
            );

            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;

        Debug.Log(
            $"[Photon] Iniciando Game. " +
            $"Hunter: Actor " +
            $"{RoleManager.GetKillerActorNumber()}"
        );

        PhotonNetwork.LoadLevel("Game");
    }

    public string GetCurrentRoomCode()
    {
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom != null)
        {
            return PhotonNetwork.CurrentRoom.Name;
        }

        return "";
    }
}