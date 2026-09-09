using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class RouletteManager : MonoBehaviourPunCallbacks
{
    [Header("UI de la ruleta")]
    [SerializeField] private GameObject roulettePanel;
    [SerializeField] private TMP_Text rouletteText;

    [Header("Duración")]
    [SerializeField] private float rouletteDuration = 3.5f;
    [SerializeField] private float resultDuration = 1.5f;

    [Header("Animación")]
    [SerializeField] private int visualSteps = 28;

    private Coroutine rouletteCoroutine;

    public bool IsRunning => rouletteCoroutine != null;

    private void Start()
    {
        if (roulettePanel != null)
            roulettePanel.SetActive(false);

        if (RoleManager.HasKiller)
            BeginRouletteFromRoomProperties();
    }

    public bool TryStartRoulette()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null)
            return false;

        int playerCount =
            PhotonNetwork.CurrentRoom.PlayerCount;

        if (playerCount < NetworkManager.MinPlayersToStart)
        {
            Debug.LogWarning(
                $"[Ruleta] Se necesitan al menos " +
                $"{NetworkManager.MinPlayersToStart} jugadores."
            );

            return false;
        }

        if (playerCount > NetworkManager.MaxPlayersPerRoom)
        {
            Debug.LogWarning(
                "[Ruleta] Hay demasiados jugadores."
            );

            return false;
        }

        if (RoleManager.HasKiller)
        {
            Debug.LogWarning(
                "[Ruleta] Ya existe un Hunter seleccionado."
            );

            return false;
        }

        Player[] players = GetOrderedPlayers();

        if (players.Length < NetworkManager.MinPlayersToStart ||
            players.Length > NetworkManager.MaxPlayersPerRoom)
        {
            return false;
        }

        System.Random seedGenerator =
            new System.Random();

        int seed =
            seedGenerator.Next(1, int.MaxValue);

        System.Random roleRandom =
            new System.Random(seed);

        int selectedIndex =
            roleRandom.Next(0, players.Length);

        int killerActorNumber =
            players[selectedIndex].ActorNumber;

        double startTime =
            PhotonNetwork.Time + 0.75;

        // Cerramos la sala para que nadie entre
        // durante la selección.
        PhotonNetwork.CurrentRoom.IsOpen = false;

        // ÚNICA selección real del Hunter.
        RoleManager.SetRole(
            killerActorNumber,
            startTime,
            seed
        );

        Debug.Log(
            $"[Ruleta] Master seleccionó al Actor " +
            $"{killerActorNumber} como Hunter."
        );

        return true;
    }

    public override void OnRoomPropertiesUpdate(
        ExitGames.Client.Photon.Hashtable changedProperties)
    {
        if (!changedProperties.ContainsKey(
                RoleManager.KillerActorNumberKey))
        {
            return;
        }

        if (RoleManager.HasKiller)
        {
            BeginRouletteFromRoomProperties();
        }
        else
        {
            ResetRouletteVisual();
        }
    }

    private void BeginRouletteFromRoomProperties()
    {
        if (!RoleManager.HasKiller)
            return;

        if (rouletteCoroutine != null)
            return;

        rouletteCoroutine =
            StartCoroutine(PlayRoulette());
    }

    private IEnumerator PlayRoulette()
    {
        double startTime =
            RoleManager.GetRouletteStartTime();

        int seed =
            RoleManager.GetRouletteSeed();

        int killerActorNumber =
            RoleManager.GetKillerActorNumber();

        if (startTime <= 0 ||
            killerActorNumber <= 0)
        {
            rouletteCoroutine = null;
            yield break;
        }

        double elapsedBeforeStart =
            PhotonNetwork.Time - startTime;

        if (elapsedBeforeStart < 0)
        {
            yield return new WaitForSeconds(
                (float)(-elapsedBeforeStart)
            );
        }

        double elapsed =
            PhotonNetwork.Time - startTime;

        if (elapsed >
            rouletteDuration + resultDuration)
        {
            rouletteCoroutine = null;

            if (PhotonNetwork.IsMasterClient &&
                PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.PlayerCount >=
                NetworkManager.MinPlayersToStart)
            {
                NetworkManager.Instance.StartGame();
            }

            yield break;
        }

        Player[] players = GetOrderedPlayers();

        if (players.Length <
                NetworkManager.MinPlayersToStart ||
            players.Length >
                NetworkManager.MaxPlayersPerRoom)
        {
            rouletteCoroutine = null;
            yield break;
        }

        if (roulettePanel != null)
            roulettePanel.SetActive(true);

        if (rouletteText != null)
            rouletteText.text =
                "SELECCIONANDO HUNTER...";

        System.Random visualRandom =
            new System.Random(seed);

        float[] intervals =
            CalculateIntervals();

        for (int i = 0; i < visualSteps; i++)
        {
            // Volvemos a obtener los jugadores por si alguien
            // abandonó durante la ruleta.
            players = GetOrderedPlayers();

            if (players.Length <
                    NetworkManager.MinPlayersToStart)
            {
                ResetRouletteVisual();

                yield break;
            }

            int selectedIndex;

            if (i == visualSteps - 1)
            {
                selectedIndex =
                    FindPlayerIndex(
                        players,
                        killerActorNumber
                    );
            }
            else
            {
                selectedIndex =
                    visualRandom.Next(
                        0,
                        players.Length
                    );
            }

            if (selectedIndex < 0)
                selectedIndex = 0;

            if (rouletteText != null)
            {
                rouletteText.text =
                    players[selectedIndex].NickName;
            }

            yield return new WaitForSeconds(
                intervals[i]
            );
        }

        string killerName =
            GetPlayerName(killerActorNumber);

        if (rouletteText != null)
        {
            rouletteText.text =
                "¡EL HUNTER ES:\n" +
                killerName +
                "!";
        }

        yield return new WaitForSeconds(
            resultDuration
        );

        rouletteCoroutine = null;

        if (PhotonNetwork.IsMasterClient &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.PlayerCount >=
                NetworkManager.MinPlayersToStart &&
            RoleManager.HasKiller)
        {
            NetworkManager.Instance.StartGame();
        }
    }

    private float[] CalculateIntervals()
    {
        float[] intervals =
            new float[Mathf.Max(visualSteps, 0)];

        if (visualSteps <= 0)
            return intervals;

        float totalWeight = 0f;

        for (int i = 0; i < visualSteps; i++)
        {
            float progress =
                visualSteps == 1
                    ? 1f
                    : (float)i / (visualSteps - 1);

            float weight =
                Mathf.Lerp(
                    0.15f,
                    1f,
                    progress * progress
                );

            intervals[i] = weight;

            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return intervals;

        float multiplier =
            rouletteDuration / totalWeight;

        for (int i = 0; i < visualSteps; i++)
        {
            intervals[i] *= multiplier;
        }

        return intervals;
    }

    private Player[] GetOrderedPlayers()
    {
        return PhotonNetwork.PlayerList
            .OrderBy(player => player.ActorNumber)
            .ToArray();
    }

    private int FindPlayerIndex(
        Player[] players,
        int actorNumber)
    {
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ActorNumber ==
                actorNumber)
            {
                return i;
            }
        }

        return -1;
    }

    private string GetPlayerName(
        int actorNumber)
    {
        Player[] players =
            PhotonNetwork.PlayerList;

        foreach (Player player in players)
        {
            if (player.ActorNumber == actorNumber)
            {
                if (!string.IsNullOrWhiteSpace(
                        player.NickName))
                {
                    return player.NickName;
                }

                return "Jugador " + actorNumber;
            }
        }

        return "Jugador " + actorNumber;
    }

    public override void OnPlayerLeftRoom(
        Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (PhotonNetwork.CurrentRoom == null)
            return;

        // Si quedan menos de 2, la partida/ruleta ya no puede continuar.
        if (PhotonNetwork.CurrentRoom.PlayerCount <
            NetworkManager.MinPlayersToStart)
        {
            Debug.LogWarning(
                "[Ruleta] Quedaron menos de 2 jugadores. " +
                "Se cancela la selección."
            );

            RoleManager.ClearRole();

            PhotonNetwork.CurrentRoom.IsOpen = true;

            ResetRouletteVisual();
        }
    }

    public override void OnMasterClientSwitched(
        Player newMasterClient)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (PhotonNetwork.CurrentRoom.PlayerCount <
            NetworkManager.MinPlayersToStart)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                RoleManager.ClearRole();

                PhotonNetwork.CurrentRoom.IsOpen = true;

                ResetRouletteVisual();
            }

            return;
        }

        // Si el Hunter ya estaba elegido, el nuevo Master
        // mantiene ese resultado.
        if (RoleManager.HasKiller)
        {
            BeginRouletteFromRoomProperties();
        }
    }

    private void ResetRouletteVisual()
    {
        if (rouletteCoroutine != null)
        {
            StopCoroutine(rouletteCoroutine);
            rouletteCoroutine = null;
        }

        if (roulettePanel != null)
            roulettePanel.SetActive(false);

        if (rouletteText != null)
            rouletteText.text = "";
    }
}