using Photon.Pun;
using TMPro;
using UnityEngine;

public class CartelTeclasProp : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text texto;

    PropTransform propLocal;

    void Update()
    {
        // Solo lo ve el Prop, y se esconde cuando termina la partida
        if (!RoleManager.HasKiller || GameManager.IsLocalAssassin() || FinDePartida.Terminada)
        {
            panel.SetActive(false);
            return;
        }

        // Buscamos MI Prop (el que controlo en esta compu)
        if (propLocal == null)
        {
            PropTransform[] todos = FindObjectsByType<PropTransform>(FindObjectsSortMode.None);
            foreach (PropTransform p in todos)
            {
                if (p.photonView.IsMine) propLocal = p;
            }
        }

        if (propLocal == null)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(true);

        if (propLocal.IsFrozen)
            texto.text = "R (mantener): Rotar · Q: Descongelar";
        else if (propLocal.IsDisguised())
            texto.text = "E: Cambiar de objeto · Q: Congelar";
        else
            texto.text = "E: Transformarse";
    }
}