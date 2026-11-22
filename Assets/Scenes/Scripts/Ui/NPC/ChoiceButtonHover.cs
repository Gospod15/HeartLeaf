using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ChoiceButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.35f, 1f);

    private void Awake()
    {
        if (labelText == null)
            labelText = GetComponentInChildren<TextMeshProUGUI>();

        SetColor(normalColor);
    }

    private void OnEnable()
    {
        SetColor(normalColor);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetColor(normalColor);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetColor(hoverColor);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetColor(normalColor);
    }

    private void SetColor(Color color)
    {
        if (labelText != null)
            labelText.color = color;
    }
}