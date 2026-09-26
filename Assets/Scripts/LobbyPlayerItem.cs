using UnityEngine;
using TMPro;

// Lobi listesindeki tek bir satırı temsil eder. Kendi prefab'ınıza
// (img üzerinde isim yazan basit bir tasarım) bu scripti ekleyip
// nameText alanını ilgili TMP_Text'e bağlamanız yeterlidir.
public class LobbyPlayerItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    public void SetPlayerName(string playerName)
    {
        if (nameText != null)
        {
            nameText.text = playerName;
        }
    }
}
