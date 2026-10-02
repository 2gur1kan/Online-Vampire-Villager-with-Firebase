using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Bu script HOST DAHİL HER CİHAZDA çalışır.
// Odayı dinler, o anki faza ve yerel oyuncunun rolüne/hayatta olma durumuna göre
// "sıra bende mi" bilgisini çıkarır ve UI'nin bağlanabileceği event'ler yayınlar.
// Faz ilerletme, bot oylama gibi HOST işleri burada YOKTUR (bkz. GameManager).
public class PlayerTurnController : MonoBehaviour
{
    public static PlayerTurnController Instance;

    private FireBaseDataBase db;
    private Room currentRoom;
    private Users localPlayer;
    private GameState? lastBroadcastState;

    // UI tarafı bu event'lere abone olup ekranı günceller.
    public event Action<GameState> OnPhaseChanged;
    public event Action<bool> OnMyTurnChanged;

    public bool CanActNow { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        db = FireBaseDataBase.Instance;
        db.OnRoomChanged += OnRoomDataChanged;
        db.StartListeningRoom(); // idempotent: host zaten başlatmışsa tekrar bir şey yapmaz.
    }

    private void OnDestroy()
    {
        if (db != null)
        {
            db.OnRoomChanged -= OnRoomDataChanged;
        }
    }

    // =====================================================
    // FIREBASE VERİ DİNLEME
    // =====================================================

    private void OnRoomDataChanged(Room room)
    {
        if (room == null || room.Players == null) return;

        currentRoom = room;
        localPlayer = room.Players.Find(p => p.UserID == db.CurrentPlayerID);

        // ÖNEMLİ SIRALAMA: CanActNow, dinleyicilere (VotePanel vb.) haber
        // vermeden ÖNCE güncellenir. Eskiden bu iki satır ters sıradaydı;
        // yani VotePanel yeni faza göre buton kararı verirken CanActNow HÂLÂ
        // ÖNCEKİ fazın değerini taşıyordu (bir sonraki Firebase güncellemesi
        // gelene kadar). Bu, özellikle rolü faz-faz değişen gerçek oy hakkı
        // durumlarında yanlış (bir faz geriden) butonların gösterilmesine
        // sebep olabiliyordu.
        UpdateTurnState(room.State);

        // OnPhaseChanged'i SADECE state gerçekten değiştiyse tetikle (aksi
        // halde oyuncu ekleme/çıkarma gibi state'i ilgilendirmeyen her
        // yazmada da tetiklenip örn. LobbyPanel'in "birazdan lobiye dön"
        // sayacını sürekli yeniden başlatabiliyordu).
        if (lastBroadcastState == room.State) return;

        lastBroadcastState = room.State;

        // ÖNEMLİ GÜVENLİK: Dinleyicilerden biri (örn. VotePanel) bir hata
        // fırlatırsa, normal bir "OnPhaseChanged?.Invoke(...)" çağrısı bu
        // hatayı fırlatan noktadan SONRAKİ TÜM dinleyicileri (GameStatusText,
        // RoleRevealPanel, LobbyPanel...) ÇALIŞTIRMADAN durur — ve bu hata
        // buraya (OnRoomDataChanged'e) kadar yükselir. Bunu önlemek için
        // dinleyicileri TEK TEK, her birini kendi try/catch'i içinde
        // çağırıyoruz: biri patlasa bile diğerleri normal çalışmaya devam eder.
        if (OnPhaseChanged == null) return;

        foreach (Delegate d in OnPhaseChanged.GetInvocationList())
        {
            var handler = (Action<GameState>)d;

            try
            {
                handler(room.State);
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[OnPhaseChanged HATASI]</color> Bir dinleyici hata fırlattı, diğerleri yine de çalıştı: {e}");
            }
        }
    }

    // =====================================================
    // SIRA KONTROLÜ
    // =====================================================

    private void UpdateTurnState(GameState state)
    {
        bool canAct = false;

        if (localPlayer != null && localPlayer.IsAlive)
        {
            switch (state)
            {
                case GameState.VampireVote:
                    canAct = localPlayer.Role == RoleType.Vampire;
                    break;

                case GameState.DoctorVote:
                    canAct = localPlayer.Role == RoleType.Doctor;
                    break;

                case GameState.Voting:
                    canAct = true; // Köy oylamasında hayatta olan herkes oy verir.
                    break;
            }
        }

        if (canAct == CanActNow) return;

        CanActNow = canAct;
        OnMyTurnChanged?.Invoke(canAct);

        Debug.Log($"<color=magenta>[SIRA KONTROLÜ]</color> Faz: {state} | Rolüm: {localPlayer?.Role} | Hayatta mıyım: {localPlayer?.IsAlive} | CanActNow: {canAct}");
    }

    // =====================================================
    // OY GÖNDERME
    // =====================================================

    // UI, oyuncu bir hedef seçip onayladığında bunu çağırır.
    public void SubmitVote(string targetPlayerID)
    {
        if (!CanActNow)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Sıra sizde değilken oy verilemez.");
            return;
        }

        if (string.IsNullOrEmpty(targetPlayerID))
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Geçersiz hedef, oy gönderilmedi.");
            return;
        }

        db.VotePlayer(targetPlayerID);
        db.MarkPlayerVoted(db.CurrentPlayerID);

        // Aynı turda tekrar oy vermeyi engelle (bir sonraki faz değişiminde tekrar değerlendirilecek).
        CanActNow = false;
        OnMyTurnChanged?.Invoke(false);

        Debug.Log($"<color=cyan>[OY GÖNDERİLDİ]</color> Hedef: {targetPlayerID}");
    }

    // Sıramız olmadığı bir fazda "Geç" butonuna basıldığında çağrılır. Gerçek bir
    // hedefe oy içermez, sadece bu fazda bizden beklenen girdiyi verdiğimizi
    // işaretler ki GameManager bizi beklemeyi bıraksın.
    public void MarkPhaseAcknowledged()
    {
        db.MarkPlayerVoted(db.CurrentPlayerID);
    }

    // =====================================================
    // UI ERİŞİM YARDIMCILARI
    // =====================================================

    public Room GetCurrentRoom() => currentRoom;

    public Users GetLocalPlayer() => localPlayer;

    // Vampirseniz, diğer hayattaki vampir müttefiklerinizin isimlerini döner
    // (vampir değilseniz boş liste döner). RoleReveal fazında isteğe bağlı
    // olarak bir UI metnine bağlayıp "Müttefikleriniz: ..." gibi gösterebilirsiniz.
    public List<string> GetVampireAllyNames()
    {
        if (currentRoom == null || currentRoom.Players == null ||
            localPlayer == null || localPlayer.Role != RoleType.Vampire)
        {
            return new List<string>();
        }

        return currentRoom.Players
            .Where(p => p.Role == RoleType.Vampire && p.UserID != localPlayer.UserID)
            .Select(p => p.UserName)
            .ToList();
    }
}
