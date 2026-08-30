using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Vive en el GameObject "GameManager" de la escena Game.unity.
/// El Master Client decide aleatoriamente quién recibe el arma,
/// y lo comunica a TODOS los clientes con un RPC para que la
/// decisión sea única y compartida.
/// </summary>
public class GameManager : MonoBehaviourPun
{
    // Guardamos el ActorNumber del jugador armado. -1 = todavía no se decidió.
    public static int ArmedActorNumber = -1;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            AssignWeaponRandomly();
        }
    }

    void AssignWeaponRandomly()
    {
        Player[] players = PhotonNetwork.PlayerList;

        if (players.Length == 0) return;

        int randomIndex = Random.Range(0, players.Length);
        int chosenActorNumber = players[randomIndex].ActorNumber;

        // AllBuffered: incluso si algún cliente se conecta un poco
        // después, va a recibir este RPC igual apenas se una.
        photonView.RPC(nameof(RPC_SetArmedPlayer), RpcTarget.AllBuffered, chosenActorNumber);
    }

    [PunRPC]
    void RPC_SetArmedPlayer(int actorNumber)
    {
        ArmedActorNumber = actorNumber;
        Debug.Log($"[GameManager] El jugador armado es el Actor {actorNumber}");
    }
}