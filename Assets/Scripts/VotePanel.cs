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

    [Header("Ayarlar")]
    [Tooltip("Room'dan süre bilgisi henüz gelmediyse kullanılacak yedek süre.")]
    [SerializeField] private float fallbackCountdown = 10f;

    private const string PassButtonLabel = "Geç";

    private readonly List<Button> spawnedButtons = new List<Button>();
    private Coroutine countdownRoutine;
    private bool hasActedThisPhase;

    private void Start()
    {
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
        bool isVotePhase = state == GameState.VampireVote
                         || state == GameState.DoctorVote
                         || state == GameState.Voting;

        if (!isVotePhase)
        {
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

        // Seçenekler geri sayımın SONUNU beklemeden, panel açılır açılmaz gösterilir.
        ShowOptionButtons(state);

        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownRoutine());
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
    private IEnumerator CountdownRoutine()
    {
        float timer = ComputeRemainingSeconds();

        while (timer > 0f)
        {
            if (countdownText != null)
            {
                countdownText.text = Mathf.CeilToInt(timer).ToString();
            }

            yield return new WaitForSeconds(1f);
            timer -= 1f;
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
    // BUTON GÖSTERİMİ (ANONİMLİK MANTIĞI)
    // =====================================================

    private void ShowOptionButtons(GameState state)
    {
        ClearPlayerButtons();

        bool canAct = PlayerTurnController.Instance.CanActNow;

        Room room = PlayerTurnController.Instance.GetCurrentRoom();
        Users localPlayer = PlayerTurnController.Instance.GetLocalPlayer();

        if (room == null || room.Players == null || localPlayer == null) return;

        List<Users> others = room.Players
            .Where(p => p.IsAlive && p.UserID != localPlayer.UserID)
            .ToList();

        if (state == GameState.Voting)
        {
            // Köy oylamasında anonimlik gerekmez (herkes zaten oy kullanır);
            // ama çekimser kalmak isteyenler için hedef butonlarının YANINA
            // bir "Geç" butonu da ekleniyor.
            SetPlayerNameButtons(others);
            AddPassButton();
        }
        else if (canAct)
        {
            SetPlayerNameButtons(others);
        }
        else
        {
            SetPassButtons(others.Count);
        }
    }

    // Sıra bizdeyken: oyuncu isimlerinin yazdığı gerçek hedef butonları.
    private void SetPlayerNameButtons(List<Users> targets)
    {
        foreach (Users target in targets)
        {
            Button newButton = Instantiate(playerButtonPrefab, playerButtonContainer);
            newButton.gameObject.SetActive(true);
            newButton.GetComponentInChildren<TMP_Text>().text = target.UserName;

            string targetID = target.UserID; // closure için yerel kopya
            newButton.onClick.AddListener(() => OnPlayerButtonClicked(targetID));

            spawnedButtons.Add(newButton);
        }
    }

    private void AddPassButton()
    {
        Button newButton = Instantiate(playerButtonPrefab, playerButtonContainer);
        newButton.gameObject.SetActive(true);
        newButton.GetComponentInChildren<TMP_Text>().text = PassButtonLabel;

        newButton.onClick.AddListener(OnPassButtonClicked);

        spawnedButtons.Add(newButton);
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
            newButton.GetComponentInChildren<TMP_Text>().text = PassButtonLabel;

            newButton.onClick.AddListener(OnPassButtonClicked);

            spawnedButtons.Add(newButton);
        }
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
