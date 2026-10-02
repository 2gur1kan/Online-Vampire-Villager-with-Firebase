using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Oyun ilk açıldığında gösterilen panel. Oyuncu ismini girer, "Onayla" butonuna
// basar, isim yerel oyuncu olarak sisteme kaydedilir ve ConnectPanel açılır.
public class NameEntryPanel : MonoBehaviour
{
    [Header("Panel Referansları")]
    [SerializeField] private GameObject nameEntryPanelRoot;

    [Header("UI Referansları")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text botUnlockBTNText;

    // Yerel oyuncunun ismi. ConnectPanel bu ismi Users objesine yazarken kullanır.
    public static string LocalPlayerName { get; private set; }

    private void Start()
    {
        // Önceden isim girilmişse (uygulama tekrar açıldığında) input alanını doldur.
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            nameInputField.text = PlayerPrefs.GetString("PlayerName");
        }

        SelectName();
    }

    // "Onayla" butonuna bağlanır. Input alanındaki ismi sisteme kaydeder ve
    // yerel oyuncuyu (kendimizi) etiketler.
    public void SelectName()
    {
        string enteredName = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(enteredName))
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> İsim boş olamaz.");
            return;
        }

        LocalPlayerName = enteredName;
        PlayerPrefs.SetString("PlayerName", enteredName);

        Debug.Log($"<color=cyan>[İSİM KAYDEDİLDİ]</color> {enteredName} (Ben)");
    }

    // Botları açıp kapatabilmemiz için bir buton işlevi
    public void SetBots()
    {
        BOTManager.Instance.UnlockBots = !BOTManager.Instance.UnlockBots;
        UpdateButtonText();
    }

    // Botları ayarlayan butonun üzerindeki yazıyı ayarlamak için bir fonksiyon
    private void UpdateButtonText()
    {
        if (botUnlockBTNText == null) return;

        if (BOTManager.Instance.UnlockBots)
        {
            botUnlockBTNText.text = "Botlar: Açık";
        }
        else
        {
            botUnlockBTNText.text = "Botlar: Kapalı";
        }
    }
}
