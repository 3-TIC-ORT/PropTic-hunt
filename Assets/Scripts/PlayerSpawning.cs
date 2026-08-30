using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Vive en la escena Game.unity, en un GameObject "GameManager".
/// Instancia al jugador local en uno de los dos Spawn Points
/// usando PhotonNetwork.Instantiate, para que quede sincronizado
/// con el otro cliente automáticamente.
/// </summary>
public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Puntos de aparición")]
    public Transform spawnPoint1;
    public Transform spawnPoint2;

    void Start()
    {
        // Elegimos el spawn point según seamos el 1er o 2do jugador
        // que entró a la sala. ActorNumber empieza en 1.
        Transform chosenSpawn = (PhotonNetwork.LocalPlayer.ActorNumber % 2 == 1)
            ? spawnPoint1
            : spawnPoint2;

        // "Player" tiene que coincidir EXACTO con el nombre del
        // prefab guardado en Assets/Resources.
        PhotonNetwork.Instantiate(
            "Player",
            chosenSpawn.position,
            chosenSpawn.rotation
        );

        Debug.Log($"[Spawn] Jugador {PhotonNetwork.LocalPlayer.ActorNumber} instanciado en {chosenSpawn.name}");
    }
}
