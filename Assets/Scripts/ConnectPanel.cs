using UnityEngine;
using TMPro;

// İsim girişinden sonra açılan panel. Oyuncu ya yeni bir oda oluşturur
// (host olur, sistem botları odaya eklenir) ya da elindeki oda koduyla
// mevcut bir odaya katılır (misafir olur).
// Odaya girdikten sonraki her şey (oyuncu listesi, oda kodu gösterimi,
// "Oyunu Başlat" butonu) artık LobbyPanel'in sorumluluğundadır.
public class ConnectPanel : MonoBehaviour
{
    [Header("Panel Referansları")]
    [SerializeField] private GameObject connectPanelRoot;
    [SerializeField] private GameObject lobbyPanelRoot;

    [Header("UI Referansları")]
    [SerializeField] private TMP_InputField roomCodeInputField;

    private FireBaseDataBase db;

    private void Start()
    {
        db = FireBaseDataBase.Instance;
    }

    // "Oda Oluştur" butonuna bağlanır. Yeni oda açar, kendimizi host olarak
    // ekler ve sistem botlarını odaya katıp oyunu başlatır.
    public void CreateRoomBTN()
    {
        string roomCode = GenerateRoomCode();

        Room room = new Room
        {
            RoomID = roomCode,
            HostID = db.CurrentPlayerID,
            State = GameState.Lobby,
            TotalVote = 0,
            SelectedDeadPlayer = "",
            OldDoctorVote = ""
        };

        Users localPlayer = BuildLocalPlayer(isHost: true);

        db.CreateRoom(room);
        db.JoinRoom(room.RoomID, localPlayer);
        db.StartListeningRoom();

        Debug.Log($"<color=green>[ODA OLUŞTURULDU]</color> Oda Kodu: {roomCode}");

        OpenLobby();
    }

    // "Odaya Katıl" butonuna bağlanır. Girilen oda koduyla mevcut odaya
    // misafir olarak katılır. Faz yönetimi bu cihazda ÇALIŞMAZ; sadece
    // PlayerTurnController üzerinden odayı dinleriz.
    public void JoinRoomBTN()
    {
        string roomCode = roomCodeInputField.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Oda kodu girilmedi.");
            return;
        }

        Users localPlayer = BuildLocalPlayer(isHost: false);

        db.JoinRoom(roomCode, localPlayer);
        db.StartListeningRoom();

        Debug.Log($"<color=green>[ODAYA KATILINDI]</color> Oda Kodu: {roomCode}");

        OpenLobby();
    }

    private Users BuildLocalPlayer(bool isHost)
    {
        return new Users
        {
            UserID = db.CurrentPlayerID,
            UserName = NameEntryPanel.LocalPlayerName,
            Role = RoleType.Villager,
            IsAlive = true,
            VoteCount = 0,
            IsHost = isHost
        };
    }

    private string GenerateRoomCode()
    {
        return UnityEngine.Random.Range(1000, 9999).ToString();
    }

    private void OpenLobby()
    {
        connectPanelRoot.SetActive(false);
        lobbyPanelRoot.SetActive(true);
    }
}
