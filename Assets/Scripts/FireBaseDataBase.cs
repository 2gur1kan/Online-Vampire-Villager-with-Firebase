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

    // Oda de�i�ti�inde (state, oyuncular, oylar...) t�m dinleyicilere haber verir.
    // Birden fazla script (GameManager + PlayerTurnController) ayn� anda dinleyebilir.
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

            Debug.Log("<color=green>Firebase Ba�lant�s� Ba�ar�l�!</color>");
        }
        else
        {
            Debug.LogError($"Firebase ba��ml�l�klar� ��z�lemedi: {status}");
        }
    }

    // =====================================================
    // ODA & OYUNCU OLU�TURMA ��LEMLER�
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

        // OYUNCU PRESENCE: Cihaz aniden ba�lant�y� keserse (��k��, �rt d���n uygulama kapanmas�,
        // a� kopmas�) bu i�lem client taraf�nda �al��mayaca�� i�in Firebase sunucusuna
        // "bu client koparsa oyuncuyu sil" talimat�n� �nceden veriyoruz. B�ylece oda listesi
        // her zaman ger�ekten ba�l� oyuncular� yans�t�r.
        playerRef.OnDisconnect().RemoveValue();

        Debug.Log($"Oyuncu Eklendi: {player.UserName} ({player.UserID})");
    }

    // =====================================================
    // OYUN DURUMU VE VER� G�NCELLEME METODLARI
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
    // OYLAMA ��LEMLER� (TRANSACTION)
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
                }
            }
        });
    }

    // =====================================================
    // F�REBASE VER� OKUMA METODLARI (TEK SEFERL�K �EK��)
    // =====================================================

    // Anl�k, tek seferlik oda verisi �ekmek i�in (�rn. dinleyici ba�lamadan �nceki ilk y�kleme).
    // D�ng� i�inde tekrar tekrar �a��rarak "polling" yapmak yerine StartListeningRoom kullan�n.
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

        // Players Listesini Doldurma
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

                room.Players.Add(user);
            }
        }

        return room;
    }

    // =====================================================
    // ODA D�NLEY�C�S� (REAL-TIME, POLL�NG YOK)
    // =====================================================

    // Oday� bir kez dinlemeye ba�lar. Her de�i�iklikte (state, oyuncular, oylar)
    // OnRoomChanged event'i t�m abonelere (GameManager, PlayerTurnController vb.) g�venli �ekilde yay�n yapar.
    // Idempotent'tir: zaten dinleniyorsa tekrar �a�r�l�rsa hi�bir �ey yapmaz.
    public void StartListeningRoom()
    {
        if (string.IsNullOrEmpty(CurrentRoomID))
        {
            Debug.LogWarning("Oda ID'si bo� oldu�u i�in dinleyici ba�lat�lamad�.");
            return;
        }

        if (isListeningRoom) return;

        roomValueChangedHandler = (sender, args) =>
        {
            if (args.DatabaseError != null)
            {
                Debug.LogError($"Oda dinleme hatas�: {args.DatabaseError.Message}");
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
    // AYRILMA ��LEMLER�
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
            Debug.Log("Host ayr�ld��� i�in oda silindi.");
        }
        else
        {
            await db.Child("Rooms")
                    .Child(CurrentRoomID)
                    .Child("Players")
                    .Child(CurrentPlayerID)
                    .RemoveValueAsync();
            Debug.Log("Oyuncu odadan ayr�ld�.");
        }

        StopListeningRoom();
    }

    private void OnApplicationQuit()
    {
        // Not: Bu ��a��r� normal ��k��ta �al���r, ancak Task tamamlanmadan uygulama kapanabilir.
        // Ger�ek g�vence CreatePlayerInRoom i�indeki OnDisconnect().RemoveValue() �a�r�s�d�r
        // (��k��, a� kopmas� veya �ok�) ��nk� o, sunucu taraf�nda garanti �al���r.
        LeaveRoom();
    }
}
