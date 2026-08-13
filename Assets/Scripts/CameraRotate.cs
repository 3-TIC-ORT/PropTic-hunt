using UnityEngine;
 
/// <summary>
/// Rota la cámara del lobby suavemente sobre su propio eje (por defecto Y) en 360° continuos.
/// La cámara permanece FIJA en posición: solo rota, nunca se traslada.
/// Está pensado para funcionar sin cambios cuando reemplaces el escenario placeholder
/// por el ambiente 3D definitivo, ya que no depende de nada del entorno.
///
/// Colocar este componente en el GameObject de la Camera del lobby (ej: "LobbyCamera").
/// </summary>
[DisallowMultipleComponent]
public class CameraAutoRotate : MonoBehaviour
{
    [Header("Configuración de rotación")]
    [Tooltip("Velocidad de rotación en grados por segundo. Valores bajos (4-8) dan una sensación cinematográfica.")]
    [SerializeField] private float rotationSpeed = 6f;
 
    [Tooltip("Eje local sobre el que rota la cámara. Vector3.up = eje vertical (lo normal para un giro 360°).")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
 
    [Header("Suavizado de inicio")]
    [Tooltip("Si está activo, la rotación arranca desde velocidad 0 y acelera suavemente hasta la velocidad configurada.")]
    [SerializeField] private bool useEaseIn = true;
 
    [Tooltip("Tiempo (en segundos) que tarda en alcanzar la velocidad de rotación completa.")]
    [SerializeField] private float easeInDuration = 2f;
 
    private float _easeTimer = 0f;
    private float _currentSpeedMultiplier = 0f;
    private bool _isRotating = true;
 
    private void Update()
    {
        if (!_isRotating) return;
 
        float effectiveSpeed = rotationSpeed;
 
        if (useEaseIn && _currentSpeedMultiplier < 1f)
        {
            _easeTimer += Time.deltaTime;
            _currentSpeedMultiplier = Mathf.SmoothStep(0f, 1f, _easeTimer / Mathf.Max(easeInDuration, 0.001f));
            effectiveSpeed *= _currentSpeedMultiplier;
        }
 
        // Rotación en espacio local: garantiza que la cámara gire sobre SU PROPIO eje,
        // sin desplazarse ni orbitar alrededor de otro punto.
        transform.Rotate(rotationAxis, effectiveSpeed * Time.deltaTime, Space.Self);
    }
 
    /// <summary>
    /// Permite pausar/reanudar la rotación desde otro script.
    /// Útil, por ejemplo, si más adelante abrís un submenú de configuración
    /// y querés "congelar" la cámara mientras está abierto.
    /// </summary>
    public void SetRotating(bool value)
    {
        _isRotating = value;
    }
 
    /// <summary>Permite cambiar la velocidad de rotación en runtime (ej: desde un menú de opciones).</summary>
    public void SetRotationSpeed(float newSpeed)
    {
        rotationSpeed = newSpeed;
    }
}
 
