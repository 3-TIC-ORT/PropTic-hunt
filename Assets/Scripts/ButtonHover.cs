using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
 
/// <summary>
/// Efecto visual de hover y click para los botones del lobby: escala suave
/// y cambio de color, sin depender de plugins externos (solo Unity nativo).
///
/// Colocar este componente en el MISMO GameObject que cada Button
/// (ej: "Button_CreateRoom" y "Button_JoinRoom").
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Escala")]
    [Tooltip("Multiplicador de escala al pasar el cursor por encima.")]
    [SerializeField] private float hoverScale = 1.08f;
 
    [Tooltip("Velocidad de interpolación de la animación (más alto = más rápido).")]
    [SerializeField] private float animationSpeed = 8f;
 
    [Header("Color (opcional, requiere asignar la Image del botón)")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.3f, 1f);
 
    private Vector3 _baseScale;
    private Vector3 _targetScale;
    private Color _targetColor;
 
    private void Awake()
    {
        _baseScale = transform.localScale;
        _targetScale = _baseScale;
        _targetColor = normalColor;
 
        if (targetImage != null)
        {
            targetImage.color = normalColor;
        }
    }
 
    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * animationSpeed);
 
        if (targetImage != null)
        {
            targetImage.color = Color.Lerp(targetImage.color, _targetColor, Time.deltaTime * animationSpeed);
        }
    }
 
    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = _baseScale * hoverScale;
        _targetColor = hoverColor;
    }
 
    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = _baseScale;
        _targetColor = normalColor;
    }
 
    public void OnPointerClick(PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(ClickPulse());
    }
 
    private System.Collections.IEnumerator ClickPulse()
    {
        // Pequeño "hundimiento" al hacer click para dar feedback táctil.
        transform.localScale = _baseScale * 0.94f;
        yield return new WaitForSeconds(0.08f);
        _targetScale = _baseScale * hoverScale;
    }
}
 
