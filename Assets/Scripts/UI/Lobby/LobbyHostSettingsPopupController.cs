using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyHostSettingsPopupController : MonoBehaviour
{
    private const string HostSettingsTitle = "Host Settings";

    [SerializeField] private TMP_Text titleText;

    private void Awake()
    {
        if (titleText != null)
        {
            titleText.text = HostSettingsTitle;
        }
    }
}