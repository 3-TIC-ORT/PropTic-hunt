using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class RegistroManager : MonoBehaviour
{
    public TMP_InputField campoMail;
    public TMP_InputField campoNombre;
    public TMP_InputField campoContrasena;

    string url = "https://api-prop-tic-hunt.vercel.app/auth/registro";

    [System.Serializable]
    public class DatosRegistro
    {
        public string nombre;
        public string mail;
        public string contrasena;
    }

    public void IniciarRegistro()
    {
        StartCoroutine(HacerRegistro());
    }

    IEnumerator HacerRegistro()
    {
        DatosRegistro datos = new DatosRegistro();
        datos.nombre = campoNombre.text;
        datos.mail = campoMail.text;
        datos.contrasena = campoContrasena.text;
        string json = JsonUtility.ToJson(datos);

        UnityWebRequest pedido = UnityWebRequest.Post(url, json, "application/json");
        yield return pedido.SendWebRequest();

        if (pedido.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Registro correcto. Ya podes iniciar sesion.");
        }
        else
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
        }
    }
}