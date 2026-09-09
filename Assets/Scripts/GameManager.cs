using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static int ArmedActorNumber { get; private set; } = -1;

    [Header("UI")]
    [SerializeField] private TMP_Text localRoleText;

    private void Awake()
    {
        // El valor ya fue seleccionado en RoomLobby.
        // No hacemos ningún Random aquí.
        ArmedActorNumber =
            RoleManager.GetKillerActorNumber();
    }

    private void Start()
    {
        ApplyRole();
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable changedProperties)
    {
        if (changedProperties.ContainsKey(
                RoleManager.KillerActorNumberKey))
        {
            ApplyRole();
        }
    }

    private void ApplyRole()
    {
        ArmedActorNumber =
            RoleManager.GetKillerActorNumber();

        if (localRoleText == null)
        {
            Debug.LogWarning(
                "[GameManager] No hay texto asignado " +
                "para mostrar el rol local."
            );

            return;
        }

        if (ArmedActorNumber <= 0)
        {
            localRoleText.text =
                "ROL NO ASIGNADO";

            return;
        }

        if (IsLocalAssassin())
        {
            localRoleText.text =
                "ASESINO";
        }
        else
        {
            localRoleText.text =
                "ESCAPISTA";
        }
    }

    public static bool IsAssassinActor(
        int actorNumber)
    {
        return
            ArmedActorNumber > 0 &&
            ArmedActorNumber == actorNumber;
    }

    public static bool IsLocalAssassin()
    {
        if (PhotonNetwork.LocalPlayer == null)
            return false;

        return IsAssassinActor(
            PhotonNetwork.LocalPlayer.ActorNumber
        );
    }
}