using Photon.Pun;
using UnityEngine;

/// <summary>
/// Puerta que se abre una sola vez y queda abierta.
/// Poner en la puerta (con PhotonView y Animator) y llamar a Open()
/// cuando termine el contador de salida del cazador.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class DoorController : MonoBehaviourPun
{
    [SerializeField] private Animator animator;
    [SerializeField] private string openTrigger = "open";

    private bool isOpen;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Se puede llamar desde cualquier cliente: solo el Master
    /// manda el RPC, así la puerta se abre una única vez para todos.
    /// </summary>
    public void Open()
    {
        if (isOpen)
            return;

        if (!PhotonNetwork.IsMasterClient)
            return;

        // AllBuffered: si alguien entra tarde, la puerta ya aparece abierta.
        photonView.RPC(nameof(RPC_Open), RpcTarget.AllBuffered);
    }

    [PunRPC]
    private void RPC_Open()
    {
        if (isOpen)
            return;

        isOpen = true;

        if (animator != null)
            animator.SetTrigger(openTrigger);
        else
            Debug.LogWarning("[DoorController] Falta el Animator de la puerta.");
    }
}