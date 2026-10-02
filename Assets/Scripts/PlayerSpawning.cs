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
        Debug.LogError("[Spawn] Se necesitan al menos 2 jugadores.");
        return;
    }

    if (players.Length > 6)
    {
        Debug.LogError("[Spawn] No pueden existir más de 6 jugadores.");
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

    int hunterActor = RoleManager.GetKillerActorNumber();
    int localActor = PhotonNetwork.LocalPlayer.ActorNumber;

    int spawnIndex;

    if (hunterActor <= 0)
    {
        // Sin rol asignado (por ejemplo, si probás la escena Game
        // directamente): se usa el orden por actor, como antes.
        Debug.LogWarning("[Spawn] No hay Hunter asignado. Se usa el orden por actor.");
        spawnIndex = System.Array.FindIndex(
            players, p => p.ActorNumber == localActor);
    }
    else if (localActor == hunterActor)
    {
        // El Hunter siempre aparece en el SpawnPoint 1.
        spawnIndex = 0;
    }
    else
    {
        // Los escapistas ocupan los spawns 2 a 6, en orden de actor.
        Player[] escapists =
            players
                .Where(p => p.ActorNumber != hunterActor)
                .ToArray();

        int escapistIndex = System.Array.FindIndex(
            escapists, p => p.ActorNumber == localActor);

        spawnIndex = escapistIndex < 0 ? -1 : escapistIndex + 1;
    }

    if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length)
    {
        Debug.LogError("[Spawn] No se pudo calcular el spawn del jugador local.");
        return;
    }

    Transform chosenSpawn = spawnPoints[spawnIndex];

    if (chosenSpawn == null)
    {
        Debug.LogError($"[Spawn] Falta configurar SpawnPoint {spawnIndex + 1}.");
        return;
    }

    Vector3 spawnPosition =
        GetGroundedSpawnPosition(chosenSpawn.position);

    PhotonNetwork.Instantiate(
        "Player",
        spawnPosition,
        chosenSpawn.rotation
    );

    Debug.Log(
        $"[Spawn] Actor {localActor} " +
        $"({(localActor == hunterActor ? "HUNTER" : "ESCAPISTA")}) " +
        $"aparece en {chosenSpawn.name} -> {spawnPosition}"
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