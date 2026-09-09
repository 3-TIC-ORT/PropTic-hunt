using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Puntos de aparición")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;
    [SerializeField] private Transform spawnPoint3;
    [SerializeField] private Transform spawnPoint4;
    [SerializeField] private Transform spawnPoint5;
    [SerializeField] private Transform spawnPoint6;

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
            return;

        Player[] players =
            PhotonNetwork.PlayerList
                .OrderBy(player => player.ActorNumber)
                .ToArray();

        if (players.Length < 2)
        {
            Debug.LogError(
                "[Spawn] Se necesitan al menos 2 jugadores."
            );

            return;
        }

        if (players.Length > 6)
        {
            Debug.LogError(
                "[Spawn] No pueden existir más de 6 jugadores."
            );

            return;
        }

        Transform[] spawnPoints =
        {
            spawnPoint1,
            spawnPoint2,
            spawnPoint3,
            spawnPoint4,
            spawnPoint5,
            spawnPoint6
        };

        // Comprobamos que haya suficientes puntos.
        for (int i = 0; i < players.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError(
                    $"[Spawn] Falta configurar " +
                    $"SpawnPoint {i + 1}."
                );

                return;
            }
        }

        int localIndex = -1;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ActorNumber ==
                PhotonNetwork.LocalPlayer.ActorNumber)
            {
                localIndex = i;
                break;
            }
        }

        if (localIndex < 0)
        {
            Debug.LogError(
                "[Spawn] No se encontró al jugador local."
            );

            return;
        }

        Transform chosenSpawn =
            spawnPoints[localIndex];

        PhotonNetwork.Instantiate(
            "Player",
            chosenSpawn.position,
            chosenSpawn.rotation
        );

        Debug.Log(
            $"[Spawn] Actor " +
            $"{PhotonNetwork.LocalPlayer.ActorNumber} " +
            $"aparece en {chosenSpawn.name}"
        );
    }
}