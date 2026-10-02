using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Oylama panelini yönetir. HOST DAHİL HER CİHAZDA çalışır.
//
// SENKRON SÜRE: Geri sayım, bu cihazın kendi saatine değil, GameManager'ın
// Room'a yazdığı SUNUCU zamanına (PhaseStartTimeMillis/PhaseDurationSeconds)
// göre hesaplanır. Böylece ağ gecikmesi farklı olsa bile TÜM cihazlar aynı
// gerçek ana kadar geri sayar.
//
// ANONİMLİK: Sıra bizde olsun ya da olmasın, ekranda HER ZAMAN aynı sayıda
// ve aynı düzende buton görünür (aynı hedef havuzunun boyutu kadar). Sıra
// bizdeyse butonlarda gerçek isimler yazar ve tıklayınca oy gönderir; sıra
// bizde değilse HEPSİNDE "Geç" yazar ve hangisine basılırsa basılsın sadece
// "işlemimizi yaptık" olarak işaretler. Böylece yan yana oturan biri, ekrana
// bakarak kimin vampir/doktor olduğunu buton SAYISINDAN bile çıkaramaz.
//
// Panel süre dolmadan kapanmaz; bir seçim yapsak da butonlar sadece devre
// dışı bırakılır, panel açık kalır. Süre dolunca hiç seçim yapılmadıysa
// otomatik olarak "Geç" ile aynı şekilde işaretlenir.
public class VotePanel : MonoBehaviour
{
    [Header("Panel Referansları")]
    [SerializeField] private GameObject votePanelRoot;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text phaseTitleText;

    [Header("Oyuncu Butonları")]
    [SerializeField] private Transform playerButtonContainer;
    [SerializeField] private Button playerButtonPrefab;

    [Header("Süre Uzatma (Sadece Host, Köy Oylamasında)")]
    [Tooltip("Köy oylamasında, diğer karakter butonlarıyla AYNI prefab'tan, " +
             "sadece host'un ekranına dinamik olarak eklenecek '+20' butonuna " +
             "her basışta köy oylamasına eklenecek süre.")]
    [SerializeField] private float extendTimeSeconds = 20f;

    [Header("Ayarlar")]
    [Tooltip("Room'dan süre bilgisi henüz gelmediyse kullanılacak yedek süre.")]
    [SerializeField] private float fallbackCountdown = 10f;

    private const string PassButtonLabel = "Geç";

    private readonly List<Button> spawnedButtons = new List<Button>();
    private Button spawnedExtendButton;
    private Coroutine countdownRoutine;
    private bool hasActedThisPhase;

    private void Start()
    {
        // Bu üçü eksikse ("hiç seçenek gelmiyor" diye görünen çoğu durumun
        // asıl sebebi budur) sessizce başarısız olmak yerine şimdi, net bir
        // şekilde uyarıyoruz.
        if (votePanelRoot == null)
            Debug.LogError("<color=red>[VOTEPANEL KURULUM HATASI]</color> votePanelRoot Inspector'da boş!");
        if (playerButtonContainer == null)
            Debug.LogError("<color=red>[VOTEPANEL KURULUM HATASI]</color> playerButtonContainer Inspector'da boş! Butonlar oluşsa bile hiçbir yere yerleşmez/görünmez.");
        if (playerButtonPrefab == null)
            Debug.LogError("<color=red>[VOTEPANEL KURULUM HATASI]</color> playerButtonPrefab Inspector'da boş! Hiç buton oluşturulamaz.");

        PlayerTurnController.Instance.OnPhaseChanged += OnPhaseChanged;
        votePanelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (PlayerTurnController.Instance != null)
        {
            PlayerTurnController.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
    }

    // =====================================================
    // FAZ DİNLEME
    // =====================================================

    private void OnPhaseChanged(GameState state)
    {
        // Bu log HER faz değişiminde, hiçbir erken çıkıştan önce çalışır.
        // Eğer bir fazda "hiç buton gelmiyor" ama bu log hiç görünmüyorsa,
        // sorun VotePanel'de değil, event'in VotePanel'e hiç ulaşmamasındadır.
        Debug.Log($"<color=cyan>[VOTEPANEL] OnPhaseChanged çağrıldı: {state}</color>");

        bool isVotePhase = state == GameState.VampireVote
                         || state == GameState.DoctorVote
                         || state == GameState.Voting;

        if (!isVotePhase)
        {
            ClosePanel();
            return;
        }

        Users localPlayer = PlayerTurnController.Instance.GetLocalPlayer();

        if (localPlayer == null || !localPlayer.IsAlive)
        {
            // Ölü oyuncular oylamaya katılmaz. Ölümleri zaten LastEventMessage
            // ile herkese duyurulduğu için burada gizlenecek bir şey yok;
            // panel hiç açılmadan sadece izleyici konumunda kalırlar.
            Debug.Log($"<color=cyan>[VOTEPANEL]</color> {state} fazı için panel AÇILMADI. Sebep: " +
                      (localPlayer == null ? "localPlayer null (henüz oda verisi gelmemiş olabilir)" : "localPlayer.IsAlive = false (ölüsünüz)"));
            ClosePanel();
            return;
        }

        OpenPanelWithOptions(state);
    }

    private void OpenPanelWithOptions(GameState state)
    {
        votePanelRoot.SetActive(true);
        hasActedThisPhase = false;

        if (phaseTitleText != null)
        {
            phaseTitleText.text = GetPhaseTitle(state);
        }

        // Seçenekleri temizleyip yeni faz için baştan oluşturuyoruz
        ShowOptionButtons(state);

        // YENİ EKLENEN: Yeni oluşturulan butonların etkileşime açık olduğundan emin oluyoruz
        SetButtonsInteractable(true);

        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    // Köy oylamasına ekstra süre eklemek için host'un bastığı buton.
    // Aynı anda herkesin ekranındaki geri sayımı senkron şekilde uzatır
    // (bkz. FireBaseDataBase.ExtendCurrentPhaseTimer ve ComputeRemainingSeconds).
    public void ExtendVillageVoteTimeBTN()
    {
        Room room = PlayerTurnController.Instance.GetCurrentRoom();

        if (room == null || FireBaseDataBase.Instance.CurrentPlayerID != room.HostID)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Süreyi sadece host uzatabilir.");
            return;
        }

        FireBaseDataBase.Instance.ExtendCurrentPhaseTimer(extendTimeSeconds);
    }

    private string GetPhaseTitle(GameState state)
    {
        switch (state)
        {
            case GameState.VampireVote: return "Gece çöktü...";
            case GameState.DoctorVote: return "Gece çöktü...";
            case GameState.Voting: return "Köy oylaması";
            default: return "";
        }
    }

    // =====================================================
    // SUNUCU SENKRONLU GERİ SAYIM
    // =====================================================

    private float ComputeRemainingSeconds()
    {
        Room room = PlayerTurnController.Instance.GetCurrentRoom();

        if (room == null || room.PhaseDurationSeconds <= 0f)
        {
            return fallbackCountdown;
        }

        double serverNow = FireBaseDataBase.Instance.GetServerNowMillis();
        double elapsedMillis = serverNow - room.PhaseStartTimeMillis;
        float remaining = room.PhaseDurationSeconds - (float)(elapsedMillis / 1000.0);

        return Mathf.Max(0f, remaining);
    }

    // Bu döngü sadece görsel geri sayımı ve sürenin SONUNDA seçeneklerin
    // kaybolup panelin kapanmasını yönetir; seçenekleri açmaz (onlar zaten açık).
    //
    // ÖNEMLİ: Her saniye kalan süreyi SIFIRDAN, Room'daki güncel
    // PhaseDurationSeconds'a göre yeniden hesaplıyoruz (yerel bir sayaçtan
    // azaltmak yerine). Host, ExtendCurrentPhaseTimer ile süreyi ortasında
    // uzatırsa, bir sonraki tik bunu otomatik olarak yakalar — ekstra bir
    // "değişti mi?" kontrolüne gerek kalmadan geri sayım kendiliğinden uzar.
    private IEnumerator CountdownRoutine()
    {
        float remaining = ComputeRemainingSeconds();

        while (remaining > 0f)
        {
            if (countdownText != null)
            {
                countdownText.text = Mathf.CeilToInt(remaining).ToString();
            }

            yield return new WaitForSeconds(1f);

            remaining = ComputeRemainingSeconds();
        }

        if (countdownText != null)
        {
            countdownText.text = "";
        }

        // Süre doldu: eğer bu fazda hiç seçim yapılmadıysa (ne hedef seçildi ne
        // bir "Geç" butonuna basıldı) otomatik olarak aynı şekilde işaretle ki
        // GameManager sonsuza kadar bizi beklemesin.
        if (!hasActedThisPhase)
        {
            hasActedThisPhase = true;
            PlayerTurnController.Instance.MarkPhaseAcknowledged();
        }

        ClosePanel();
    }

    // =====================================================
    // BUTON GÖSTERİMİ
    // =====================================================

    private void ShowOptionButtons(GameState state)
    {
        ClearPlayerButtons();

        Room room = PlayerTurnController.Instance.GetCurrentRoom();
        Users localPlayer = PlayerTurnController.Instance.GetLocalPlayer();

        if (room == null || room.Players == null || localPlayer == null) return;

        List<Users> others = room.Players
            .Where(p => p.IsAlive && p.UserID != localPlayer.UserID)
            .ToList();

        bool canAct = PlayerTurnController.Instance.CanActNow;

        Debug.Log($"<color=magenta>[VOTEPANEL]</color> Faz: {state} | Rolüm: {localPlayer.Role} | CanActNow: {canAct} | Diğer oyuncular: {others.Count}");

        if (state == GameState.Voting)
        {
            // Köy oylamasında herkes oy kullanabilir, anonimlik gerekmez.
            SetPlayerNameButtons(others);
            AddPassButton();
            AddExtendTimeButtonIfHost();
        }
        else
        {
            // Gece fazları (VampireVote / DoctorVote)
            if (canAct)
            {
                Debug.Log("<color=magenta>[VOTEPANEL]</color> Gerçek hedef butonları gösteriliyor.");
                SetPlayerNameButtons(others);
            }
            else
            {
                Debug.Log("<color=magenta>[VOTEPANEL]</color> 'Geç' (anonim) butonları gösteriliyor.");
                SetPassButtons(others.Count);
            }
        }
    }

    // Sıra bizdeyken: oyuncu isimlerinin yazdığı gerçek hedef butonları.
    private void SetPlayerNameButtons(List<Users> targets)
    {
        foreach (Users target in targets)
        {
            Button newButton = Instantiate(playerButtonPrefab, playerButtonContainer);
            newButton.gameObject.SetActive(true);
            SetButtonLabel(newButton, target.UserName);

            string targetID = target.UserID; // closure için yerel kopya
            newButton.onClick.AddListener(() => OnPlayerButtonClicked(targetID));

            spawnedButtons.Add(newButton);
        }
    }

    // Sıra bizde değilken: gerçek oyuncu sayısıyla AYNI sayıda buton, ama
    // hepsinde "Geç" yazar. Ekranın görünümü, sıra kimdeyse onunkiyle birebir
    // aynı kalır; hangi butona basılırsa basılsın sadece "geçildiğini" işaretler.
    private void SetPassButtons(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Button newButton = Instantiate(playerButtonPrefab, playerButtonContainer);
            newButton.gameObject.SetActive(true);
            SetButtonLabel(newButton, PassButtonLabel);

            newButton.onClick.AddListener(OnPassButtonClicked);

            spawnedButtons.Add(newButton);
        }
    }

    // Köy oylamasında hedef butonlarının yanına eklenen TEK "Geç" (çekimser) butonu.
    private void AddPassButton()
    {
        Button newButton = Instantiate(playerButtonPrefab, playerButtonContainer);
        newButton.gameObject.SetActive(true);
        SetButtonLabel(newButton, PassButtonLabel);

        newButton.onClick.AddListener(OnPassButtonClicked);

        spawnedButtons.Add(newButton);
    }

    // Köy oylamasında, diğer karakter butonlarıyla AYNI prefab'tan, AYNI
    // container'a, SADECE host'un ekranına "+20" yazan bir buton ekler.
    // Diğer oyuncular bunu hiç görmez (isHost değilse metot hiçbir şey yapmaz).
    //
    // ÖNEMLİ: Bu buton BİLİNÇLİ olarak spawnedButtons listesine EKLENMEZ.
    // Çünkü host kendi oyunu kullandığında/Geç dediğinde SetButtonsInteractable(false)
    // çalışıyor — eğer bu listede olsaydı host kendi seçimini yaptıktan SONRA
    // artık süre uzatamazdı. Böylece host, kendi oyunu kullanmış olsa bile
    // süre dolana kadar istediği an +20 basabilir.
    private void AddExtendTimeButtonIfHost()
    {
        Room room = PlayerTurnController.Instance.GetCurrentRoom();
        bool isHost = room != null && FireBaseDataBase.Instance.CurrentPlayerID == room.HostID;

        if (!isHost) return;

        spawnedExtendButton = Instantiate(playerButtonPrefab, playerButtonContainer);
        spawnedExtendButton.gameObject.SetActive(true);
        SetButtonLabel(spawnedExtendButton, $"+{Mathf.RoundToInt(extendTimeSeconds)}");

        spawnedExtendButton.onClick.AddListener(ExtendVillageVoteTimeBTN);
    }

    // ÖNEMLİ: GetComponentInChildren, prefabınızdaki metin "empty object" gibi
    // bir ara obje PASİF durumdaysa (includeInactive vermezseniz) hiçbir şey
    // bulamaz ve doğrudan .text yazmaya çalışmak NullReferenceException
    // fırlatırdı. Bu hata, ShowOptionButtons'ın ORTASINDA fırlayıp kalan
    // butonların hiç oluşmamasına VE PlayerTurnController'daki olay
    // zincirinin kesilmesine (bkz. PlayerTurnController.OnRoomDataChanged)
    // yol açabiliyordu — "bazı fazlarda hiçbir seçenek gelmiyor" hatasının
    // en olası sebebi buydu. Artık includeInactive:true ile aranıyor ve
    // bulunamazsa sessizce çökmek yerine açık bir hata basılıyor.
    private void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            Debug.LogError("<color=red>[HATA]</color> playerButtonPrefab içinde TMP_Text bulunamadı! " +
                           "Butonun altında (doğrudan ya da bir alt obje içinde) bir " +
                           "TextMeshPro - Text (UI) olduğundan emin olun.");
            return;
        }

        label.text = text;
    }

    private void OnPlayerButtonClicked(string targetID)
    {
        if (hasActedThisPhase) return; // aynı fazda ikinci kez tıklamayı engelle
        hasActedThisPhase = true;

        PlayerTurnController.Instance.SubmitVote(targetID);

        // Panel KAPANMAZ; sadece butonlar devre dışı bırakılır. Böylece süre
        // dolana kadar ekran, henüz seçim yapmamış biriyle aynı görünür.
        SetButtonsInteractable(false);
    }

    private void OnPassButtonClicked()
    {
        if (hasActedThisPhase) return;
        hasActedThisPhase = true;

        PlayerTurnController.Instance.MarkPhaseAcknowledged();
        SetButtonsInteractable(false);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (Button btn in spawnedButtons)
        {
            btn.interactable = interactable;
        }
    }

    private void ClearPlayerButtons()
    {
        foreach (Button btn in spawnedButtons)
        {
            Destroy(btn.gameObject);
        }
        spawnedButtons.Clear();

        if (spawnedExtendButton != null)
        {
            Destroy(spawnedExtendButton.gameObject);
            spawnedExtendButton = null;
        }
    }

    private void ClosePanel()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        ClearPlayerButtons();
        votePanelRoot.SetActive(false);
    }
}
