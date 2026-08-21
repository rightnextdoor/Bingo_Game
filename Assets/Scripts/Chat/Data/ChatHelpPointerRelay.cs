using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ChatHelpPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ChatHeaderController headerController;

    private void Awake()
    {
        if (headerController == null)
        {
            headerController = GetComponentInParent<ChatHeaderController>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        headerController?.NotifyHelpPointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        headerController?.NotifyHelpPointerExit();
    }
}
