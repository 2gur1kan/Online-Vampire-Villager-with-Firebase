using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// "Lobi" panelinde çalışır. Odaya kimlerin katıldığını canlı olarak listeler,
// oda kodunu gösterir ve "Oyunu Başlat" butonunu yönetir.
//
// POLLING YOK: FireBaseDataBase.OnRoomChanged event'i her tetiklendiğinde
// (biri katıldığında, ayrıldığında, herhangi bir veri değiştiğinde) liste
// otomatik olarak yeniden çizilir. InvokeRepeating ile sürekli sorgu atmaya
// gerek yoktur; Firebase zaten değişiklikleri anlık olarak bu event ile bildirir.
public class LobbyPanel : MonoBehaviour
{
    [Header("Panel Referansları")]
    [SerializeField] private GameObject lobbyPanelRoot;
    [SerializeField] private GameObject gamePanelRoot;

    [Header("UI Referansları")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private LobbyPlayerItem playerListItemPrefab;

    [Header("Ayarlar")]
    [Tooltip("Oyun bitince kazanan mesajının, Lobi'ye dönmeden önce ekranda ne kadar süre kalacağı.")]
    [SerializeField] private float endGameMessageDisplaySeconds = 4f;

    private FireBaseDataBase db;
    private Room currentRoom;
    private readonly List<LobbyPlayerItem> spawnedItems = new List<LobbyPlayerItem>();
    private Coroutine endGameRoutine;

    private void Start()
    {
        db = FireBaseDataBase.Instance;
        db.OnRoomChanged += OnRoomDataChanged;

        // GameManager.OnGameEnded gibi yerel bir event YERİNE, herkeste zaten
        // çalışan PlayerTurnController.OnPhaseChanged dinlenir; oyun bitince
        // host'un yazdığı GameState.EndGame TÜM cihazlara Firebase üzerinden ulaşır.
        if (PlayerTurnController.Instance != null)
        {
            PlayerTurnController.Instance.OnPhaseChanged += OnPhaseChanged;
        }
    }

    private void OnDestroy()
    {
        if (db != null)
        {
            db.OnRoomChanged -= OnRoomDataChanged;
        }

        if (PlayerTurnController.Instance != null)
        {
            PlayerTurnController.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
    }

    // =====================================================
    // REAKTİF OYUNCU LİSTESİ
    // =====================================================

    private void OnRoomDataChanged(Room room)
    {
        if (room == null || room.Players == null) return;

        currentRoom = room;

        if (roomCodeText != null)
        {
            roomCodeText.text = room.RoomID;
        }

        RefreshPlayerList(room.Players);
    }

    private void RefreshPlayerList(List<Users> players)
    {
        foreach (LobbyPlayerItem item in spawnedItems)
        {
            Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        foreach (Users player in players)
        {
            LobbyPlayerItem item = Instantiate(playerListItemPrefab, playerListContainer);
            item.SetPlayerName(player.UserName);
            spawnedItems.Add(item);
        }
    }

    // =====================================================
    // OYUNU BAŞLATMA / BİTİRME
    // =====================================================

    // "Oyunu Başlat" butonuna bağlanır. Sadece odanın HOST'u için anlamlıdır;
    // misafir bir oyuncu yanlışlıkla basarsa hiçbir şey olmaz.
    //
    // ÖNEMLİ: Burada panel geçişini (Lobi'yi kapat, GamePanel'i aç) DOĞRUDAN
    // yapmıyoruz — bu sadece host'un kendi cihazında olurdu, misafirler hiç
    // haberdar olmazdı (EndGame'deki hatanın aynısı, ters yönde). Bunun yerine
    // GameManager, oyunu gerçekten başlatırken (RoleReveal state'ine geçerken)
    // bunu Firebase'e yazıyor; TÜM cihazlar (host dahil) aşağıdaki
    // OnPhaseChanged üzerinden aynı anda geçiş yapıyor.
    public void StartGameBTN()
    {
        if (currentRoom == null || db.CurrentPlayerID != currentRoom.HostID)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Oyunu sadece host başlatabilir.");
            return;
        }

        GameManager.Instance.HostGameWithSystemBots();
    }

    // Firebase'den gelen faz değişikliklerine göre HERKESTE (host dahil) aynı
    // anda çalışır: RoleReveal → oyun başladı, GamePanel'e geç; EndGame →
    // oyun bitti, Lobi'ye dön.
    private void OnPhaseChanged(GameState state)
    {
        if (state == GameState.RoleReveal)
        {
            lobbyPanelRoot.SetActive(false);
            gamePanelRoot.SetActive(true);
            return;
        }

        if (state == GameState.EndGame)
        {
            // Kazanan mesajı (GameStatusText zaten Room.LastEventMessage'ı
            // gösteriyor) okunmaya fırsat kalmadan ekran çat diye Lobi'ye
            // dönmesin diye burada bir süre bekliyoruz. GamePanel bu süre
            // boyunca açık kalır, VotePanel zaten kapalıdır (oylama fazı
            // değil), yani ekranda sadece kazanan mesajı görünür.
            if (endGameRoutine != null) StopCoroutine(endGameRoutine);
            endGameRoutine = StartCoroutine(ReturnToLobbyAfterDelay());
        }
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSeconds(endGameMessageDisplaySeconds);

        gamePanelRoot.SetActive(false);
        lobbyPanelRoot.SetActive(true);
    }
}
