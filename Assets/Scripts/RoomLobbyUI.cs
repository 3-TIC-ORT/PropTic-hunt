using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomLobbyUI : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerListEntryPrefab;
    [SerializeField] private Button startGameButton;

    private readonly List<GameObject> _spawnedEntries = new List<GameObject>();

    private void Start()
    {
        roomCodeText.text = PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.Name
            : "";

        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => RefreshPlayerList();
    public override void OnPlayerLeftRoom(Player otherPlayer) => RefreshPlayerList();
    public override void OnMasterClientSwitched(Player newMasterClient) => RefreshStartButton();

    private void RefreshPlayerList()
    {
        foreach (var entry in _spawnedEntries) Destroy(entry);
        _spawnedEntries.Clear();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            var entry = Instantiate(playerListEntryPrefab, playerListContainer);
            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = player.NickName + (player.IsMasterClient ? " (host)" : "");
            _spawnedEntries.Add(entry);
        }
    }

    private void RefreshStartButton()
    {
        // Solo el MasterClient (el que creó la sala, o quien herede el rol si se va)
        // puede ver/usar el botón de empezar partida.
        startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
    }

    // Enganchar al OnClick() de Button_StartGame
    public void OnStartGamePressed()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        NetworkManager.Instance.StartGame();
    }
}
