using UnityEngine;
using TMPro;

// GamePanel içindeki bilgi metnini yönetir. Debug.Log SADECE host'un Unity
// konsolunda görünür; oyuncular hiçbir build'de bunu göremez. Bu yüzden
// gece sonucu, oylama sonucu ve oyun bitişi gibi mesajlar host tarafından
// Firebase'e yazılır (bkz. GameManager), bu script de db.OnRoomChanged
// üzerinden bunları TÜM cihazlarda okuyup ekrana basar.
public class GameStatusText : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    private FireBaseDataBase db;

    private string lastNightMessage = "";
    private string lastVoteMessage = "";
    private string gameOverMessage = "";

    private void Start()
    {
        db = FireBaseDataBase.Instance;
        db.OnRoomChanged += OnRoomDataChanged;
    }

    private void OnDestroy()
    {
        if (db != null)
        {
            db.OnRoomChanged -= OnRoomDataChanged;
        }
    }

    // GamePanel her aktif olduğunda (yeni oyun başladığında) çalışır ve
    // bir önceki oyundan kalan metni/önbelleği temizler.
    private void OnEnable()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }

        lastNightMessage = "";
        lastVoteMessage = "";
        gameOverMessage = "";
    }

    private void OnRoomDataChanged(Room room)
    {
        if (room == null) return;

        if (!string.IsNullOrEmpty(room.LastNightMessage) && room.LastNightMessage != lastNightMessage)
        {
            lastNightMessage = room.LastNightMessage;
            SetLatest(lastNightMessage);
        }

        if (!string.IsNullOrEmpty(room.LastVoteMessage) && room.LastVoteMessage != lastVoteMessage)
        {
            lastVoteMessage = room.LastVoteMessage;
            SetLatest(lastVoteMessage);
        }

        if (!string.IsNullOrEmpty(room.GameOverMessage) && room.GameOverMessage != gameOverMessage)
        {
            gameOverMessage = room.GameOverMessage;
            SetLatest(gameOverMessage);
        }
    }

    // Metni BİRİKTİRMEZ; ekranda her zaman sadece en son olay görünür.
    private void SetLatest(string line)
    {
        if (statusText != null)
        {
            statusText.text = line;
        }
    }
}
