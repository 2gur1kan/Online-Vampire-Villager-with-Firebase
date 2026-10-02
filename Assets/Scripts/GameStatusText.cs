using UnityEngine;
using TMPro;

// GamePanel içindeki bilgi metnini yönetir. Debug.Log SADECE host'un Unity
// konsolunda görünür; oyuncular hiçbir build'de bunu göremez. Bu yüzden her
// faz geçişinde (roller dağıtıldı, gece çöktü, köy oylaması başladı, gece
// sonucu, oylama sonucu, oyun bitişi) host tarafından Firebase'e
// (Room.LastEventMessage) yazılan durum mesajını bu script okuyup ekrana basar.
public class GameStatusText : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    private FireBaseDataBase db;

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
    // bir önceki oyundan kalan metni temizler.
    private void OnEnable()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    private void OnRoomDataChanged(Room room)
    {
        if (room == null || statusText == null) return;

        if (!string.IsNullOrEmpty(room.LastEventMessage))
        {
            statusText.text = room.LastEventMessage;
        }
    }
}
