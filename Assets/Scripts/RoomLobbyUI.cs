using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RouletteManager))]
public class RoomLobbyUI : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerListEntryPrefab;
    [SerializeField] private Button startGameButton;

    private readonly List<GameObject> _spawnedEntries =
        new List<GameObject>();

    private RouletteManager rouletteManager;

    private void Awake()
    {
        rouletteManager =
            GetComponent<RouletteManager>();
    }

    private void Start()
    {
        if (roomCodeText != null)
        {
            roomCodeText.text =
                PhotonNetwork.CurrentRoom != null
                    ? PhotonNetwork.CurrentRoom.Name
                    : "";
        }

        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnPlayerEnteredRoom(
        Player newPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnPlayerLeftRoom(
        Player otherPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnMasterClientSwitched(
        Player newMasterClient)
    {
        RefreshStartButton();
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable changedProperties)
    {
        RefreshStartButton();
    }

    private void RefreshPlayerList()
    {
        foreach (GameObject entry in _spawnedEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        _spawnedEntries.Clear();

        if (!PhotonNetwork.InRoom)
            return;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject entry =
                Instantiate(
                    playerListEntryPrefab,
                    playerListContainer
                );

            TMP_Text text =
                entry.GetComponentInChildren<TMP_Text>();

            if (text != null)
            {
                text.text =
                    player.NickName +
                    (player.IsMasterClient
                        ? " (host)"
                        : "");
            }

            _spawnedEntries.Add(entry);
        }
    }

    private void RefreshStartButton()
    {
        if (startGameButton == null)
            return;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null)
        {
            startGameButton.gameObject.SetActive(false);
            return;
        }

        bool isMaster =
            PhotonNetwork.IsMasterClient;

        int playerCount =
            PhotonNetwork.CurrentRoom.PlayerCount;

        bool canStart =
            isMaster &&
            playerCount >=
                NetworkManager.MinPlayersToStart &&
            playerCount <=
                NetworkManager.MaxPlayersPerRoom &&
            !RoleManager.HasKiller &&
            !rouletteManager.IsRunning;

        startGameButton.gameObject.SetActive(
            isMaster
        );

        startGameButton.interactable =
            canStart;
    }

    public void OnStartGamePressed()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null)
            return;

        int playerCount =
            PhotonNetwork.CurrentRoom.PlayerCount;

        if (playerCount <
            NetworkManager.MinPlayersToStart)
        {
            Debug.LogWarning(
                $"[RoomLobby] Se necesitan al menos " +
                $"{NetworkManager.MinPlayersToStart} jugadores."
            );

            return;
        }

        if (playerCount >
            NetworkManager.MaxPlayersPerRoom)
        {
            Debug.LogWarning(
                "[RoomLobby] Se superó el máximo de jugadores."
            );

            return;
        }

        rouletteManager.TryStartRoulette();

        RefreshStartButton();
    }
}