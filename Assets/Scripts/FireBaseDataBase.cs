using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Database;

public class FireBaseDataBase : MonoBehaviour
{
    public static FireBaseDataBase Instance;

    private DatabaseReference db;

    public string CurrentRoomID;
    public string CurrentPlayerID;

    // Oda değiştiğinde (state, oyuncular, oylar) tüm dinleyicilere haber verir.
    // Birden fazla script (GameManager + PlayerTurnController) aynı anda dinleyebilir.
    public event Action<Room> OnRoomChanged;

    private EventHandler<ValueChangedEventArgs> roomValueChangedHandler;
    private bool isListeningRoom;

    // Firebase sunucusunun saati ile bu cihazın kendi saati arasındaki fark
    // (ms). Bu sayede her cihaz, kendi yerel saatinden bağımsız olarak aynı
    // "sunucu şu an" tahminini hesaplayabilir (bkz. GetServerNowMillis).
    private double serverTimeOffsetMillis;

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (!PlayerPrefs.HasKey("PlayerID"))
        {
            PlayerPrefs.SetString("PlayerID", Guid.NewGuid().ToString());
        }

        CurrentPlayerID = PlayerPrefs.GetString("PlayerID");

        var status = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (status == DependencyStatus.Available)
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            app.Options.DatabaseUrl = new Uri("https://test-c92d6-default-rtdb.firebaseio.com/");
            db = FirebaseDatabase.DefaultInstance.RootReference;

            ListenServerTimeOffset();

            Debug.Log("<color=green>Firebase bağlantısı başarılı!</color>");
        }
        else
        {
            Debug.LogError($"Firebase bağımlılıkları çözülemedi: {status}");
        }
    }

    // =====================================================
    // SUNUCU ZAMANI (TÜM CİHAZLARDA SENKRON GERİ SAYIM İÇİN)
    // =====================================================

    // Firebase'in ".info/serverTimeOffset" özel konumu, bu cihazın saatiyle
    // sunucunun saati arasındaki farkı (ms) sürekli günceller. Ağ gecikmesi
    // veya cihaz saati yanlış olsa bile, buradan hesaplanan "sunucu şu an"
    // tüm cihazlarda aynı çıkar.
    private void ListenServerTimeOffset()
    {
        FirebaseDatabase.DefaultInstance.GetReference(".info/serverTimeOffset").ValueChanged += (sender, args) =>
        {
            if (args.DatabaseError != null) return;

            if (args.Snapshot.Exists && args.Snapshot.Value != null)
            {
                serverTimeOffsetMillis = Convert.ToDouble(args.Snapshot.Value);
            }
        };
    }

    // Bu cihazın tahmin ettiği, Firebase sunucusundaki şu anki zaman (ms,
    // Unix epoch). VotePanel gibi UI'ler geri sayımı buna göre hesaplar.
    public double GetServerNowMillis()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + serverTimeOffsetMillis;
    }

    // Bir fazın (vampir/doktor/köy oylaması) başlangıç anını SUNUCU
    // saatiyle (ServerValue.Timestamp) ve süresini Room'a yazar. Böylece
    // her cihaz, kendi Firebase güncellemesini ne zaman aldığından bağımsız
    // olarak aynı bitiş anını hesaplar ve geri sayımlar senkron olur.
    public void StartPhaseTimer(float durationSeconds)
    {
        var updates = new Dictionary<string, object>
        {
            { "PhaseStartTimeMillis", ServerValue.Timestamp },
            { "PhaseDurationSeconds", durationSeconds }
        };

        db.Child("Rooms").Child(CurrentRoomID).UpdateChildrenAsync(updates);
    }

    // =====================================================
    // ODA & OYUNCU OLUŞTURMA İŞLEMLERİ
    // =====================================================

    public void CreateRoom(Room roomObj)
    {
        CurrentRoomID = roomObj.RoomID;
        db.Child("Rooms").Child(roomObj.RoomID).SetRawJsonValueAsync(JsonUtility.ToJson(roomObj));
    }

    public void JoinRoom(string roomID, Users player)
    {
        CurrentRoomID = roomID;
        CreatePlayerInRoom(player);
    }

    public void CreatePlayerInRoom(Users player)
    {
        DatabaseReference playerRef = db.Child("Rooms")
                                         .Child(CurrentRoomID)
                                         .Child("Players")
                                         .Child(player.UserID);

        playerRef.SetRawJsonValueAsync(JsonUtility.ToJson(player));

        // OYUNCU PRESENCE: Cihaz aniden bağlantıyı keserse (uygulamanın kapanması,
        // ağ kopması vb.) bu işlem client tarafında çalışmayacağı için Firebase
        // sunucusuna "bu client koparsa oyuncuyu sil" talimatını önceden veriyoruz.
        // Böylece oda listesi her zaman gerçekten bağlı oyuncuları yansıtır.
        playerRef.OnDisconnect().RemoveValue();

        Debug.Log($"Oyuncu eklendi: {player.UserName} ({player.UserID})");
    }

    // =====================================================
    // OYUN DURUMU VE VERİ GÜNCELLEME METODLARI
    // =====================================================

    public void ChangeGameState(GameState gs)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("State")
          .SetValueAsync((int)gs);
    }

    public void SetPlayerRole(string playerID, RoleType role)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("Players")
          .Child(playerID)
          .Child("Role")
          .SetValueAsync((int)role);
    }

    public void SetPlayerAlive(string playerID, bool alive)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("Players")
          .Child(playerID)
          .Child("IsAlive")
          .SetValueAsync(alive);
    }

    public void SetSelectedDeadPlayer(string playerID)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("SelectedDeadPlayer")
          .SetValueAsync(playerID ?? "");
    }

    public void SetOldDoctorVote(string playerID)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("OldDoctorVote")
          .SetValueAsync(playerID ?? "");
    }

    // =====================================================
    // OYUNCULARA GÖSTERİLECEK DURUM MESAJLARI
    // =====================================================
    // Debug.Log SADECE host'un Unity konsolunda görünür; oyuncular hiçbir
    // build'de bunu göremez. Bu yüzden gece/oylama sonucu ve oyun bitişi gibi
    // bilgileri ayrıca Firebase'e yazıyoruz ki GameStatusText tüm cihazlarda
    // bunları okuyup ekrana basabilsin.

    public void SetLastEventMessage(string message)
    {
        db.Child("Rooms").Child(CurrentRoomID).Child("LastEventMessage").SetValueAsync(message ?? "");
    }

    // Yeni bir oyun başlarken bir önceki oyundan kalan mesajı temizler.
    public void ClearStatusMessages()
    {
        SetLastEventMessage("");
    }

    // =====================================================
    // YENİ BİR OYUNA HAZIRLIK (AYNI ODADA TEKRAR BAŞLATMA)
    // =====================================================
    // Aynı odada "Oyunu Başlat" tekrar basılırsa, bir önceki oyundan kalan
    // sistem botları ve ölü durumu birikmesin diye bu iki metot kullanılır.

    // Odadaki tüm BOT oyuncuları siler (gerçek oyunculara dokunmaz). Yeni bir
    // oyun başlarken önce bu çağrılıp, sonra taze botlar eklenmelidir; aksi
    // halde her "Oyunu Başlat"ta eski botların üstüne bir set daha eklenir.
    public async Task RemoveAllBotsAsync()
    {
        if (string.IsNullOrEmpty(CurrentRoomID)) return;

        DataSnapshot playersSnapshot = await db.Child("Rooms").Child(CurrentRoomID).Child("Players").GetValueAsync();

        if (!playersSnapshot.Exists) return;

        List<Task> removeTasks = new List<Task>();

        foreach (var child in playersSnapshot.Children)
        {
            bool isBot = child.Child("IsBot").Value != null && Convert.ToBoolean(child.Child("IsBot").Value);

            if (isBot)
            {
                removeTasks.Add(
                    db.Child("Rooms").Child(CurrentRoomID).Child("Players").Child(child.Key).RemoveValueAsync()
                );
            }
        }

        await Task.WhenAll(removeTasks);
    }

    // Odadaki tüm (gerçek) oyuncuları tekrar "hayatta" yapar. Bir önceki
    // oyunda ölen bir oyuncu, aynı odada yeni bir oyun başlayınca tekrar
    // oynayabilsin diye bu, yeni oyun başında çağrılır.
    public async Task ResetAllPlayersAliveAsync()
    {
        if (string.IsNullOrEmpty(CurrentRoomID)) return;

        DataSnapshot playersSnapshot = await db.Child("Rooms").Child(CurrentRoomID).Child("Players").GetValueAsync();

        if (!playersSnapshot.Exists) return;

        List<Task> updateTasks = new List<Task>();

        foreach (var child in playersSnapshot.Children)
        {
            updateTasks.Add(
                db.Child("Rooms").Child(CurrentRoomID).Child("Players").Child(child.Key).Child("IsAlive").SetValueAsync(true)
            );
        }

        await Task.WhenAll(updateTasks);
    }

    // =====================================================
    // OYLAMA İŞLEMLERİ (TRANSACTION)
    // =====================================================

    public void VotePlayer(string playerID)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("Players")
          .Child(playerID)
          .Child("VoteCount")
          .RunTransaction(mutable =>
          {
              int vote = 0;
              if (mutable.Value != null)
              {
                  vote = Convert.ToInt32(mutable.Value);
              }
              mutable.Value = vote + 1;
              return TransactionResult.Success(mutable);
          });

        AddTotalVote();
    }

    // Oyu VEREN kişiyi (hedefi değil) bu fazda "işlemini yaptı" olarak işaretler.
    // GameManager bekleme döngülerinde gerçek oyuncuların (bot olmayanların)
    // hepsi bunu işaretlemeden faza devam etmez. Gerçek bir oy içermeyen
    // "Geç" aksiyonu için de bu metot çağrılır (bkz. PlayerTurnController.MarkPhaseAcknowledged).
    public void MarkPlayerVoted(string voterPlayerID)
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("Players")
          .Child(voterPlayerID)
          .Child("HasVotedThisPhase")
          .SetValueAsync(true);
    }

    public void AddTotalVote()
    {
        db.Child("Rooms")
          .Child(CurrentRoomID)
          .Child("TotalVote")
          .RunTransaction(mutable =>
          {
              int vote = 0;
              if (mutable.Value != null)
              {
                  vote = Convert.ToInt32(mutable.Value);
              }
              mutable.Value = vote + 1;
              return TransactionResult.Success(mutable);
          });
    }

    // ÖNEMLİ: Bu metot artık TAMAMEN beklenir (async Task). Eski sürüm
    // arka planda (GetValueAsync().ContinueWith(...), beklenmeden) çalışıyordu;
    // bu sıfırlama isteği bazen bir sonraki fazın oyları/bot oyları ATILDIKTAN
    // SONRA tamamlanıp onları tekrar 0'a çekiyordu — "saldırı oluyor ama kimse
    // ölmüyor" hatasının asıl sebebi buydu. GameManager artık bu Task
    // tamamlanmadan bir sonraki faza geçmiyor.
    public async Task ResetRoomVotesAsync()
    {
        if (string.IsNullOrEmpty(CurrentRoomID)) return;

        List<Task> resetTasks = new List<Task>
        {
            db.Child("Rooms").Child(CurrentRoomID).Child("TotalVote").SetValueAsync(0)
        };

        DataSnapshot playersSnapshot = await db.Child("Rooms").Child(CurrentRoomID).Child("Players").GetValueAsync();

        if (playersSnapshot.Exists)
        {
            foreach (var child in playersSnapshot.Children)
            {
                resetTasks.Add(
                    db.Child("Rooms").Child(CurrentRoomID).Child("Players").Child(child.Key).Child("VoteCount").SetValueAsync(0)
                );

                resetTasks.Add(
                    db.Child("Rooms").Child(CurrentRoomID).Child("Players").Child(child.Key).Child("HasVotedThisPhase").SetValueAsync(false)
                );
            }
        }

        await Task.WhenAll(resetTasks);
    }

    // =====================================================
    // FIREBASE VERİ OKUMA METODLARI (TEK SEFERLİK ÇEKİŞ)
    // =====================================================

    // Anlık, tek seferlik oda verisi çekmek için (örn. dinleyici başlamadan önceki
    // ilk yükleme). Döngü içinde tekrar tekrar çağırarak "polling" yapmak yerine
    // StartListeningRoom kullanın.
    public async Task<Room> GetRoomAsync()
    {
        if (string.IsNullOrEmpty(CurrentRoomID)) return null;

        DataSnapshot snapshot = await db.Child("Rooms").Child(CurrentRoomID).GetValueAsync();

        return ParseRoomSnapshot(snapshot);
    }

    private Room ParseRoomSnapshot(DataSnapshot snapshot)
    {
        if (!snapshot.Exists) return null;

        Room room = new Room();

        room.RoomID = snapshot.Child("RoomID").Value?.ToString();
        room.HostID = snapshot.Child("HostID").Value?.ToString();

        room.TotalVote = snapshot.Child("TotalVote").Value == null
            ? 0
            : Convert.ToInt32(snapshot.Child("TotalVote").Value);

        room.State = snapshot.Child("State").Value == null
            ? GameState.Lobby
            : (GameState)Convert.ToInt32(snapshot.Child("State").Value);

        room.SelectedDeadPlayer = snapshot.Child("SelectedDeadPlayer").Value?.ToString();
        room.OldDoctorVote = snapshot.Child("OldDoctorVote").Value?.ToString();

        room.LastEventMessage = snapshot.Child("LastEventMessage").Value?.ToString() ?? "";

        room.PhaseStartTimeMillis = snapshot.Child("PhaseStartTimeMillis").Value == null
            ? 0
            : Convert.ToDouble(snapshot.Child("PhaseStartTimeMillis").Value);

        room.PhaseDurationSeconds = snapshot.Child("PhaseDurationSeconds").Value == null
            ? 0f
            : Convert.ToSingle(snapshot.Child("PhaseDurationSeconds").Value);

        // Oyuncu Listesini Doldurma
        room.Players = new List<Users>();
        var playersSnapshot = snapshot.Child("Players");

        if (playersSnapshot.Exists)
        {
            foreach (var child in playersSnapshot.Children)
            {
                Users user = new Users();

                user.UserID = child.Child("UserID").Value?.ToString();
                user.UserName = child.Child("UserName").Value?.ToString();

                if (child.Child("Role").Value != null)
                    user.Role = (RoleType)Convert.ToInt32(child.Child("Role").Value);

                if (child.Child("IsAlive").Value != null)
                    user.IsAlive = Convert.ToBoolean(child.Child("IsAlive").Value);

                if (child.Child("VoteCount").Value != null)
                    user.VoteCount = Convert.ToInt32(child.Child("VoteCount").Value);

                if (child.Child("IsHost").Value != null)
                    user.IsHost = Convert.ToBoolean(child.Child("IsHost").Value);

                if (child.Child("IsBot").Value != null)
                    user.IsBot = Convert.ToBoolean(child.Child("IsBot").Value);

                if (child.Child("HasVotedThisPhase").Value != null)
                    user.HasVotedThisPhase = Convert.ToBoolean(child.Child("HasVotedThisPhase").Value);

                room.Players.Add(user);
            }
        }

        return room;
    }

    // =====================================================
    // ODA DİNLEYİCİSİ (REAL-TIME, POLLING YOK)
    // =====================================================

    // Odayı bir kez dinlemeye başlar. Her değişiklikte (state, oyuncular, oylar)
    // OnRoomChanged event'i tüm abonelere (GameManager, PlayerTurnController vb.)
    // güvenli şekilde yayın yapar. Idempotent'tir: zaten dinleniyorsa tekrar
    // çağrılırsa hiçbir şey yapmaz.
    public void StartListeningRoom()
    {
        if (string.IsNullOrEmpty(CurrentRoomID))
        {
            Debug.LogWarning("Oda ID'si boş olduğu için dinleyici başlatılamadı.");
            return;
        }

        if (isListeningRoom) return;

        roomValueChangedHandler = (sender, args) =>
        {
            if (args.DatabaseError != null)
            {
                Debug.LogError($"Oda dinleme hatası: {args.DatabaseError.Message}");
                return;
            }

            Room room = ParseRoomSnapshot(args.Snapshot);
            OnRoomChanged?.Invoke(room);
        };

        db.Child("Rooms").Child(CurrentRoomID).ValueChanged += roomValueChangedHandler;
        isListeningRoom = true;
    }

    public void StopListeningRoom()
    {
        if (!isListeningRoom || roomValueChangedHandler == null) return;

        db.Child("Rooms").Child(CurrentRoomID).ValueChanged -= roomValueChangedHandler;
        roomValueChangedHandler = null;
        isListeningRoom = false;
    }

    // =====================================================
    // AYRILMA İŞLEMLERİ
    // =====================================================

    public async void LeaveRoom()
    {
        if (string.IsNullOrEmpty(CurrentRoomID)) return;

        DataSnapshot snapshot = await db.Child("Rooms").Child(CurrentRoomID).Child("HostID").GetValueAsync();

        if (!snapshot.Exists) return;

        string hostID = snapshot.Value.ToString();

        if (hostID == CurrentPlayerID)
        {
            await db.Child("Rooms").Child(CurrentRoomID).RemoveValueAsync();
            Debug.Log("Host ayrıldığı için oda silindi.");
        }
        else
        {
            await db.Child("Rooms")
                    .Child(CurrentRoomID)
                    .Child("Players")
                    .Child(CurrentPlayerID)
                    .RemoveValueAsync();
            Debug.Log("Oyuncu odadan ayrıldı.");
        }

        StopListeningRoom();
    }

    private void OnApplicationQuit()
    {
        // Not: Bu çağrı normal çıkışta çalışır, ancak Task tamamlanmadan uygulama
        // kapanabilir. Gerçek güvence CreatePlayerInRoom içindeki
        // OnDisconnect().RemoveValue() çağrısıdır (çıkış, ağ kopması veya çökme),
        // çünkü o sunucu tarafında garanti çalışır.
        LeaveRoom();
    }
}
