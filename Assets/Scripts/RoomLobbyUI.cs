using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lista de jugadores del RoomLobby.
//
// Usa las 6 filas que ya están puestas en la escena (solo el marco) y crea
// por código lo que les falta: avatar, nombre, corona y punto verde.
// Si una fila ya tiene hijos con esos nombres (Avatar, Nombre, Corona, Punto)
// los reutiliza y les vuelve a configurar anclas, posición y tamaño.
[RequireComponent(typeof(RouletteManager))]
public class RoomLobbyUI : MonoBehaviourPunCallbacks
{
    private class RowRefs
    {
        public Image background;
        public Image avatar;
        public TMP_Text nameText;
        public Image crown;
        public Image dot;
    }

    [Header("Textos")]
    [SerializeField] private TMP_Text roomCodeText;

    [Tooltip("Se escribe antes del código. Dejalo vacío si el " +
             "'CÓDIGO:' ya es un texto aparte en tu escena.")]
    [SerializeField] private string roomCodePrefix =
        "<color=#CDB8FF>CÓDIGO: </color>";

    [Tooltip("Opcional: muestra '5 / 6'.")]
    [SerializeField] private TMP_Text playerCountText;

    [Header("Filas de jugadores")]
    [Tooltip("Arrastrá acá las 6 filas (los objetos con el marco), " +
             "de arriba hacia abajo.")]
    [SerializeField] private List<Image> rowImages = new List<Image>();

    [Tooltip("Opcional. Si no ponés filas arriba, usa los hijos con " +
             "Image de este objeto.")]
    [SerializeField] private Transform playerListContainer;

    [Header("Sprites de las filas")]
    [SerializeField] private Sprite rowNormal;      // frame_row
    [SerializeField] private Sprite rowHost;        // frame_row_host
    [SerializeField] private Sprite rowEmpty;       // frame_row_empty
    [SerializeField] private Sprite avatarFilled;   // icon_usuario
    [SerializeField] private Sprite avatarEmpty;    // icon_usuario_vacio
    [SerializeField] private Sprite crownSprite;    // icon_corona
    [SerializeField] private Sprite dotSprite;      // icon_punto

    [Header("Texto de las filas")]
    [Tooltip("Opcional. Si queda vacío usa la fuente por defecto de TMP.")]
    [SerializeField] private TMP_FontAsset nameFont;
    [SerializeField] private float nameFontSize = 28f;
    [SerializeField] private Color filledTextColor = Color.white;
    [SerializeField] private Color emptyTextColor =
        new Color(0.48f, 0.44f, 0.84f, 1f);
    [SerializeField] private string emptyText = "Esperando jugador...";

    [Header("Botones")]
    [SerializeField] private Button startGameButton;

    [Tooltip("Opcional: cartel para los que no son anfitriones.")]
    [SerializeField] private GameObject waitingForHostLabel;

    [Header("Salir")]
    [SerializeField] private string sceneOnLeave = "Lobby";

    private readonly List<RowRefs> _rows = new List<RowRefs>();
    private readonly List<Player> _ordered = new List<Player>();

    private RouletteManager rouletteManager;
    private bool _built;

    private void Awake()
    {
        rouletteManager = GetComponent<RouletteManager>();
    }

    private void Start()
    {
        if (roomCodeText != null)
        {
            roomCodeText.text =
                PhotonNetwork.CurrentRoom != null
                    ? roomCodePrefix + PhotonNetwork.CurrentRoom.Name
                    : "";
        }

        BuildRows();
        RefreshPlayerList();
        RefreshStartButton();
    }

    // =========================================================
    // CALLBACKS DE PHOTON
    // =========================================================

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // La corona pasa al nuevo anfitrión.
        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProperties)
    {
        RefreshStartButton();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(sceneOnLeave);
    }

    // =========================================================
    // FILAS
    // =========================================================

    private void BuildRows()
    {
        if (_built)
            return;

        _rows.Clear();

        List<Image> images = new List<Image>();
        Transform container = playerListContainer;

        if (rowImages != null && rowImages.Count > 0)
        {
            foreach (Image img in rowImages)
            {
                if (img != null)
                    images.Add(img);
            }

            if (container == null && images.Count > 0)
                container = images[0].transform.parent;
        }
        else if (container != null)
        {
            foreach (Transform child in container)
            {
                Image img = child.GetComponent<Image>();

                if (img != null)
                    images.Add(img);
            }
        }

        if (images.Count == 0)
        {
            Debug.LogWarning(
                "[RoomLobby] No hay filas. Arrastrá las 6 filas a " +
                "'Row Images' en RoomLobbyUI."
            );
            return;
        }

        // Si faltan filas, se duplica la primera.
        while (images.Count < NetworkManager.MaxPlayersPerRoom &&
               container != null)
        {
            GameObject clone =
                Instantiate(images[0].gameObject, container);

            clone.name = "Fila_" + (images.Count + 1);
            images.Add(clone.GetComponent<Image>());
        }

        for (int i = 0; i < images.Count; i++)
        {
            // Las filas de más (sobre el máximo) se ocultan.
            if (i >= NetworkManager.MaxPlayersPerRoom)
            {
                images[i].gameObject.SetActive(false);
                continue;
            }

            Image bg = images[i];

            RowRefs refs = new RowRefs();
            refs.background = bg;

            // Avatar: extremo izquierdo de la fila.
            refs.avatar = EnsureImage(
                bg.transform, "Avatar",
                new Vector2(0f, 0.5f),
                new Vector2(34f, 0f),
                new Vector2(42f, 42f));

            refs.nameText = EnsureText(bg.transform);

            // Corona y punto comparten el extremo derecho porque nunca
            // se muestran al mismo tiempo: corona para el anfitrión,
            // punto para los demás jugadores.
            refs.crown = EnsureImage(
                bg.transform, "Corona",
                new Vector2(1f, 0.5f),
                new Vector2(-28f, 0f),
                new Vector2(36f, 36f));

            refs.dot = EnsureImage(
                bg.transform, "Punto",
                new Vector2(1f, 0.5f),
                new Vector2(-28f, 0f),
                new Vector2(18f, 18f));

            if (crownSprite != null)
                refs.crown.sprite = crownSprite;

            if (dotSprite != null)
                refs.dot.sprite = dotSprite;

            _rows.Add(refs);
        }

        _built = true;
    }

    // Busca un hijo con ese nombre; si no existe lo crea.
    // IMPORTANTE: también actualiza el RectTransform cuando el objeto ya
    // existía en la escena. Así evitamos conservar tamaños/anclas viejas.
    private Image EnsureImage(
        Transform parent,
        string childName,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        Transform existing = parent.Find(childName);
        Image img;

        if (existing != null)
        {
            img = existing.GetComponent<Image>();

            if (img == null)
                img = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            GameObject go = new GameObject(
                childName,
                typeof(RectTransform),
                typeof(Image));

            go.transform.SetParent(parent, false);
            img = go.GetComponent<Image>();
        }

        // Se aplica tanto a objetos nuevos como a los que ya estaban creados.
        RectTransform rt = img.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        img.raycastTarget = false;
        img.preserveAspect = true;

        return img;
    }

    private TMP_Text EnsureText(Transform parent)
    {
        TMP_Text text = parent.GetComponentInChildren<TMP_Text>(true);

        if (text == null)
        {
            GameObject go = new GameObject("Nombre", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            text = go.AddComponent<TextMeshProUGUI>();
        }
        else if (text.transform.parent != parent)
        {
            // El texto debe ser hijo directo de la fila para que sus
            // márgenes se midan respecto del marco de esa fila.
            text.transform.SetParent(parent, false);
        }

        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(82f, 0f);
        rt.sizeDelta = new Vector2(-130f, 44f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        if (nameFont != null)
            text.font = nameFont;

        text.fontSize = nameFontSize;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = filledTextColor;

        return text;
    }

    private void RefreshPlayerList()
    {
        BuildRows();

        _ordered.Clear();

        if (PhotonNetwork.InRoom)
        {
            // El anfitrión siempre va primero.
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.IsMasterClient)
                    _ordered.Add(player);
            }

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient)
                    _ordered.Add(player);
            }
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (i < _ordered.Count)
            {
                Player player = _ordered[i];

                string nick =
                    string.IsNullOrEmpty(player.NickName)
                        ? "Jugador " + player.ActorNumber
                        : player.NickName;

                ApplyPlayer(_rows[i], nick, player.IsMasterClient);
            }
            else
            {
                ApplyEmpty(_rows[i]);
            }
        }

        if (playerCountText != null)
        {
            playerCountText.text =
                _ordered.Count + " / " +
                NetworkManager.MaxPlayersPerRoom;
        }
    }

    private void ApplyPlayer(RowRefs row, string nick, bool isHost)
    {
        SetSprite(row.background, isHost ? rowHost : rowNormal);
        SetSprite(row.avatar, avatarFilled);

        row.nameText.text = nick;
        row.nameText.color = filledTextColor;

        row.crown.gameObject.SetActive(isHost && crownSprite != null);
        row.dot.gameObject.SetActive(!isHost && dotSprite != null);
    }

    private void ApplyEmpty(RowRefs row)
    {
        SetSprite(row.background, rowEmpty);
        SetSprite(row.avatar, avatarEmpty);

        row.nameText.text = emptyText;
        row.nameText.color = emptyTextColor;

        row.crown.gameObject.SetActive(false);
        row.dot.gameObject.SetActive(false);
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image != null && sprite != null)
            image.sprite = sprite;
    }

    // Vista previa en el editor: crea avatar, nombre, corona y punto
    // dentro de las filas para poder acomodarlos. Click derecho sobre
    // el componente RoomLobbyUI > "Crear elementos de las filas".
    [ContextMenu("Crear elementos de las filas")]
    private void CrearElementosDeFilas()
    {
        _built = false;
        BuildRows();

        for (int i = 0; i < _rows.Count; i++)
        {
            if (i == 0)
                ApplyPlayer(_rows[i], "Martín", true);
            else if (i < 5)
                ApplyPlayer(_rows[i], "Jugador " + (i + 1), false);
            else
                ApplyEmpty(_rows[i]);
        }

        _built = false; // en Play se vuelve a armar desde cero

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            gameObject.scene);
#endif
    }

    // =========================================================
    // BOTONES
    // =========================================================

    private void RefreshStartButton()
    {
        bool isMaster =
            PhotonNetwork.InRoom &&
            PhotonNetwork.IsMasterClient;

        if (waitingForHostLabel != null)
        {
            waitingForHostLabel.SetActive(
                PhotonNetwork.InRoom && !isMaster
            );
        }

        if (startGameButton == null)
            return;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null)
        {
            startGameButton.gameObject.SetActive(false);
            return;
        }

        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        bool canStart =
            isMaster &&
            playerCount >= NetworkManager.MinPlayersToStart &&
            playerCount <= NetworkManager.MaxPlayersPerRoom &&
            !RoleManager.HasKiller &&
            !rouletteManager.IsRunning;

        startGameButton.gameObject.SetActive(isMaster);
        startGameButton.interactable = canStart;
    }

    // Botón EMPEZAR PARTIDA.
    public void OnStartGamePressed()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null)
            return;

        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        if (playerCount < NetworkManager.MinPlayersToStart)
        {
            Debug.LogWarning(
                $"[RoomLobby] Se necesitan al menos " +
                $"{NetworkManager.MinPlayersToStart} jugadores."
            );

            return;
        }

        if (playerCount > NetworkManager.MaxPlayersPerRoom)
        {
            Debug.LogWarning(
                "[RoomLobby] Se superó el máximo de jugadores."
            );

            return;
        }

        rouletteManager.TryStartRoulette();

        RefreshStartButton();
    }

    // Botón SALIR.
    public void OnLeavePressed()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            SceneManager.LoadScene(sceneOnLeave);
    }
}