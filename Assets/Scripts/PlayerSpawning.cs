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

    [Header("Ajuste de suelo")]
    [SerializeField] private float raycastHeight = 10f;
    [SerializeField] private float groundOffset = 0.05f;
    [SerializeField] private LayerMask groundMask = ~0;

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

        for (int i = 0; i < players.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError(
                    $"[Spawn] Falta configurar SpawnPoint {i + 1}."
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

        Vector3 spawnPosition =
            GetGroundedSpawnPosition(
                chosenSpawn.position
            );

        PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            chosenSpawn.rotation
        );

        Debug.Log(
            $"[Spawn] Actor " +
            $"{PhotonNetwork.LocalPlayer.ActorNumber} " +
            $"aparece en {chosenSpawn.name} " +
            $"-> posición final: {spawnPosition}"
        );
    }

    private Vector3 GetGroundedSpawnPosition(
        Vector3 originalPosition)
    {
        Vector3 rayOrigin =
            originalPosition +
            Vector3.up * raycastHeight;

        RaycastHit hit;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out hit,
            raycastHeight * 2f,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            Vector3 groundedPosition =
                originalPosition;

            // La base del CharacterController está
            // en la posición Y del objeto raíz.
            groundedPosition.y =
                hit.point.y + groundOffset;

            return groundedPosition;
        }

        Debug.LogWarning(
            "[Spawn] No se encontró suelo debajo del SpawnPoint. " +
            "Se utilizará la posición original."
        );

        return originalPosition;
    }
}