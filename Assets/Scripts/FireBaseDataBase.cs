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

            Debug.Log("<color=green>Firebase bağlantısı başarılı!</color>");
        }
        else
        {
            Debug.LogError($"Firebase bağımlılıkları çözülemedi: {status}");
        }
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

    public void SetLastNightMessage(string message)
    {
        db.Child("Rooms").Child(CurrentRoomID).Child("LastNightMessage").SetValueAsync(message ?? "");
    }

    public void SetLastVoteMessage(string message)
    {
        db.Child("Rooms").Child(CurrentRoomID).Child("LastVoteMessage").SetValueAsync(message ?? "");
    }

    public void SetGameOverMessage(string message)
    {
        db.Child("Rooms").Child(CurrentRoomID).Child("GameOverMessage").SetValueAsync(message ?? "");
    }

    // Yeni bir oyun başlarken bir önceki oyundan kalan mesajları temizler.
    public void ClearStatusMessages()
    {
        SetLastNightMessage("");
        SetLastVoteMessage("");
        SetGameOverMessage("");
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

    public void ResetRoomVotes()
    {
        if (string.IsNullOrEmpty(CurrentRoomID)) return;

        db.Child("Rooms").Child(CurrentRoomID).Child("TotalVote").SetValueAsync(0);

        db.Child("Rooms").Child(CurrentRoomID).Child("Players").GetValueAsync().ContinueWith(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                foreach (var child in task.Result.Children)
                {
                    db.Child("Rooms")
                      .Child(CurrentRoomID)
                      .Child("Players")
                      .Child(child.Key)
                      .Child("VoteCount")
                      .SetValueAsync(0);

                    db.Child("Rooms")
                      .Child(CurrentRoomID)
                      .Child("Players")
                      .Child(child.Key)
                      .Child("HasVotedThisPhase")
                      .SetValueAsync(false);
                }
            }
        });
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

        room.LastNightMessage = snapshot.Child("LastNightMessage").Value?.ToString() ?? "";
        room.LastVoteMessage = snapshot.Child("LastVoteMessage").Value?.ToString() ?? "";
        room.GameOverMessage = snapshot.Child("GameOverMessage").Value?.ToString() ?? "";

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
