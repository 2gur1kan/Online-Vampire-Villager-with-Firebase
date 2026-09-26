using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Oylama panelini yönetir. HOST DAHİL HER CİHAZDA çalışır.
//
// AKIŞ: Panel açılır açılmaz seçenekler (hedef butonları ya da "Geç") HEMEN
// görünür ve geri sayım eş zamanlı başlar. Bir seçim yapsak da (veya "Geç"e
// bassak da) panel süre dolana kadar KAPANMAZ — sadece seçilen buton hariç
// hepsi devre dışı kalır. Böylece dışarıdan bakan biri kimin ne zaman
// seçim yaptığını göremez. Süre dolunca seçenekler tamamen kaybolur ve
// panel kapanır; eğer o ana kadar hiç seçim yapılmadıysa otomatik olarak
// "Geç" ile aynı şekilde işaretlenir (GameManager sonsuza kadar beklemesin diye).
public class VotePanel : MonoBehaviour
{
    [Header("Panel Referansları")]
    [SerializeField] private GameObject votePanelRoot;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text phaseTitleText;

    [Header("Oyuncu Butonları")]
    [SerializeField] private Transform playerButtonContainer;
    [SerializeField] private Button playerButtonPrefab;
    [SerializeField] private Button geciButton; // Sıra bizde değilken gösterilen "Geç" butonu

    [Header("Ayarlar")]
    [SerializeField] private float panelOpenCountdown = 10f;

    private readonly List<Button> spawnedButtons = new List<Button>();
    private Coroutine countdownRoutine;
    private bool hasActedThisPhase;

    private void Start()
    {
        PlayerTurnController.Instance.OnPhaseChanged += OnPhaseChanged;
        geciButton.onClick.AddListener(ResumeBTN);
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
        ClearPlayerButtons();

        if (phaseTitleText != null)
        {
            phaseTitleText.text = GetPhaseTitle(state);
        }

        // Seçenekler geri sayımın SONUNU beklemeden, panel açılır açılmaz gösterilir.
        ShowRelevantButtons();

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

    // Bu döngü artık sadece görsel geri sayımı ve sürenin SONUNDA seçeneklerin
    // kaybolup panelin kapanmasını yönetir; seçenekleri açmaz (onlar zaten açık).
    private IEnumerator CountdownRoutine()
    {
        float timer = panelOpenCountdown;

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
        // "Geç"e basıldı) otomatik olarak "Geç" ile aynı şekilde işaretle ki
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

    private void ShowRelevantButtons()
    {
        bool canAct = PlayerTurnController.Instance.CanActNow;

        geciButton.gameObject.SetActive(!canAct);
        geciButton.interactable = true;

        if (!canAct) return;

        Room room = PlayerTurnController.Instance.GetCurrentRoom();
        Users localPlayer = PlayerTurnController.Instance.GetLocalPlayer();

        if (room == null || room.Players == null || localPlayer == null) return;

        List<Users> targets = room.Players
            .Where(p => p.IsAlive && p.UserID != localPlayer.UserID)
            .ToList();

        SetPlayerNameButtons(targets);
    }

    // Oyuncu isimlerinin yazdığı butonları dinamik olarak oluşturur ve tıklama
    // olaylarını buradan atar; bu atama olmadan butonlar hiçbir şey yapmaz.
    private void SetPlayerNameButtons(List<Users> targets)
    {
        ClearPlayerButtons();

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

    private void OnPlayerButtonClicked(string targetID)
    {
        if (hasActedThisPhase) return; // aynı fazda ikinci kez tıklamayı engelle
        hasActedThisPhase = true;

        PlayerTurnController.Instance.SubmitVote(targetID);

        // Panel KAPANMAZ; sadece butonlar devre dışı bırakılır. Böylece süre
        // dolana kadar ekran, henüz seçim yapmamış biriyle aynı görünür.
        SetButtonsInteractable(false);
    }

    // "Geç" butonuna bağlanır. Gerçek bir hedefe oy içermez, ama GameManager'ın
    // bizi beklemeyi bırakması için bu fazda "işlemimizi yaptığımızı" işaretler.
    // Panel yine süre dolana kadar açık kalır.
    public void ResumeBTN()
    {
        if (hasActedThisPhase) return;
        hasActedThisPhase = true;

        PlayerTurnController.Instance.MarkPhaseAcknowledged();
        geciButton.interactable = false;
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
