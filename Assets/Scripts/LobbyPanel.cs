using System;
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

    private FireBaseDataBase db;
    private Room currentRoom;
    private readonly List<LobbyPlayerItem> spawnedItems = new List<LobbyPlayerItem>();

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
    public void StartGameBTN()
    {
        if (currentRoom == null || db.CurrentPlayerID != currentRoom.HostID)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Oyunu sadece host başlatabilir.");
            return;
        }

        lobbyPanelRoot.SetActive(false);
        gamePanelRoot.SetActive(true);

        GameManager.Instance.HostGameWithSystemBots();
    }

    // Firebase'den GameState.EndGame geldiğinde HERKESTE (host dahil) çalışır
    // ve ekranı otomatik olarak lobiye döndürür.
    private void OnPhaseChanged(GameState state)
    {
        if (state != GameState.EndGame) return;

        gamePanelRoot.SetActive(false);
        lobbyPanelRoot.SetActive(true);
    }
}
