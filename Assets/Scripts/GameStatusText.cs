using System.Collections;
using UnityEngine;
using TMPro;

// GamePanel içindeki bilgi metnini yönetir. Debug.Log SADECE host'un Unity
// konsolunda görünür; oyuncular hiçbir build'de bunu göremez. Bu yüzden her
// faz geçişinde (roller dağıtıldı, gece çöktü, köy oylaması başladı, gece
// sonucu, oylama sonucu, oyun bitişi) host tarafından Firebase'e
// (Room.LastEventMessage) yazılan durum mesajını bu script okuyup ekrana basar.
//
// OYLAMA FAZLARINDA OTOMATİK SİLME: VampireVote/DoctorVote/Voting fazları
// başladığında VotePanel de aynı anda açılıyor ve aynı ekran bölgesini
// kullanıyor. Bu yüzden o fazların başlangıç mesajı sadece birkaç saniye
// görünüp kendiliğinden silinir; VotePanel'in geri kalan süre boyunca
// arkasında çakışan bir yazı kalmaz. Gece/oylama SONUÇ mesajları (Day/Result
// fazlarında yazılır) bu kurala tabi değildir — o an VotePanel zaten kapalı
// olduğu için silinmeden ekranda kalabilirler.
public class GameStatusText : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    [Tooltip("Oylama fazı başlangıç mesajının, VotePanel açılmadan önce ne kadar süre görüneceği.")]
    [SerializeField] private float voteAnnouncementVisibleSeconds = 2f;

    private FireBaseDataBase db;
    private Coroutine clearRoutine;

    private void Start()
    {
        db = FireBaseDataBase.Instance;
        db.OnRoomChanged += OnRoomDataChanged;

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

    private void OnPhaseChanged(GameState state)
    {
        bool isVotePhase = state == GameState.VampireVote
                         || state == GameState.DoctorVote
                         || state == GameState.Voting;

        if (!isVotePhase) return;

        // VotePanel de tam bu anda açılıyor; mesajı bir an göster, sonra sil.
        if (clearRoutine != null) StopCoroutine(clearRoutine);
        clearRoutine = StartCoroutine(ClearAfterDelay());
    }

    private IEnumerator ClearAfterDelay()
    {
        yield return new WaitForSeconds(voteAnnouncementVisibleSeconds);

        if (statusText != null)
        {
            statusText.text = "";
        }
    }
}
