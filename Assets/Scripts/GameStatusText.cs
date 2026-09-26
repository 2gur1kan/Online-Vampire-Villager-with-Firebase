using UnityEngine;
using TMPro;

// GamePanel içindeki bilgi metnini yönetir. Debug.Log SADECE host'un Unity
// konsolunda görünür; oyuncular hiçbir build'de bunu göremez. Bu yüzden
// gece sonucu, oylama sonucu ve oyun bitişi gibi mesajlar host tarafından
// Firebase'e (Room.LastEventMessage) yazılır (bkz. GameManager), bu script
// de db.OnRoomChanged üzerinden bunu TÜM cihazlarda okuyup ekrana basar.
//
// NOT: Eski sürüm üç ayrı alanı (gece/oylama/oyun bitişi) kendi başlarına
// "değişti mi?" diye karşılaştırıyordu; iki farklı olayın metni tesadüfen
// aynı çıkarsa (örn. iki gece üst üste "kimse saldırıya uğramadı") bu
// karşılaştırma yanlışlıkla "değişmedi" sanıp ekranı eski bir olayda takılı
// bırakabiliyordu. Artık TEK bir alan var ve her olayda ÜZERİNE yazılıyor;
// bu yüzden burada karşılaştırma yapmaya gerek yok, gelen değer doğrudan
// gösterilir — ekran her zaman Firebase'deki en güncel durumu yansıtır.
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
