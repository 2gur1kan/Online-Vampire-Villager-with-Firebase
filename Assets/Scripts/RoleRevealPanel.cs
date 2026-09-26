using UnityEngine;
using TMPro;

// Ana panelde/GamePanel'de rol tanıtım ekranını yönetir. GameState.RoleReveal
// süresince (GameManager'ın belirlediği ~3 sn boyunca) yerel oyuncunun kendi
// rolünü gösterir, sonra otomatik kapanır. Süre HOST tarafında belirlenip
// Firebase üzerinden yayınlandığı (ChangeGameState) ve herkes aynı
// PlayerTurnController.OnPhaseChanged event'ini dinlediği için, panel TÜM
// cihazlarda aynı anda açılır/kapanır — ayrı bir yerel zamanlayıcıya gerek yok.
public class RoleRevealPanel : MonoBehaviour
{
    [SerializeField] private GameObject roleRevealPanelRoot;
    [SerializeField] private TMP_Text roleText;

    private void Start()
    {
        PlayerTurnController.Instance.OnPhaseChanged += OnPhaseChanged;
        roleRevealPanelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (PlayerTurnController.Instance != null)
        {
            PlayerTurnController.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
    }

    private void OnPhaseChanged(GameState state)
    {
        if (state != GameState.RoleReveal)
        {
            roleRevealPanelRoot.SetActive(false);
            return;
        }

        Users localPlayer = PlayerTurnController.Instance.GetLocalPlayer();
        if (localPlayer == null) return;

        roleRevealPanelRoot.SetActive(true);

        if (roleText != null)
        {
            roleText.text = $"Rolünüz: {GetRoleDisplayName(localPlayer.Role)}";
        }
    }

    private string GetRoleDisplayName(RoleType role)
    {
        switch (role)
        {
            case RoleType.Vampire: return "VAMPİR";
            case RoleType.Doctor: return "DOKTOR";
            default: return "KÖYLÜ";
        }
    }
}
