using UnityEngine;
using TMPro;

public class JoinRoomPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_Text errorText;

    private void OnEnable()
    {
        NetworkManager.OnJoinRoomFailedCustom += ShowError;
        LobbyUIManager.JoinRoomRequested += OpenPanel;
    }

    private void OnDisable()
    {
        NetworkManager.OnJoinRoomFailedCustom -= ShowError;
        LobbyUIManager.JoinRoomRequested -= OpenPanel;
    }

    private void OpenPanel()
    {
        panel.SetActive(true);
        errorText.text = "";
    }

    public void OnConfirmJoinPressed()
    {
        errorText.text = "";
        NetworkManager.Instance.JoinRoomByCode(codeInput.text);
    }

    private void ShowError(string message)
    {
        errorText.text = message;
    }
}