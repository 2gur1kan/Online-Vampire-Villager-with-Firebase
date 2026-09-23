using System;
using UnityEngine;

// Bu script HOST DAH�L HER C�HAZDA �al���r.
// Oday� dinler, o an ki faza ve yerel oyuncunun rol�ne/hayatta olma durumuna g�re
// "s�ra bende mi" bilgisini ��kar�r ve UI'nin ba�lanabilece�i event'ler yay�nlar.
// Faz ilerletme, bot oylama gibi HOST i�leri burada YOKTUR (bkz. GameManager).
public class PlayerTurnController : MonoBehaviour
{
    public static PlayerTurnController Instance;

    private FireBaseDataBase db;
    private Room currentRoom;
    private Users localPlayer;

    // UI taraf� bu event'lere abone olup ekran� g�nceller.
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
        db.StartListeningRoom(); // idempotent: host zaten ba�latm��sa tekrar bir �ey yapmaz.
    }

    private void OnDestroy()
    {
        if (db != null)
        {
            db.OnRoomChanged -= OnRoomDataChanged;
        }
    }

    // =====================================================
    // F�REBASE VER� D�NLEME
    // =====================================================

    private void OnRoomDataChanged(Room room)
    {
        if (room == null || room.Players == null) return;

        currentRoom = room;
        localPlayer = room.Players.Find(p => p.UserID == db.CurrentPlayerID);

        OnPhaseChanged?.Invoke(room.State);
        UpdateTurnState(room.State);
    }

    // =====================================================
    // SIRA KONTROL�
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
                    canAct = true; // K�y oylamas�nda hayatta olan herkes oy verir.
                    break;
            }
        }

        if (canAct == CanActNow) return;

        CanActNow = canAct;
        OnMyTurnChanged?.Invoke(canAct);
    }

    // =====================================================
    // OY G�NDERME
    // =====================================================

    // UI, oyuncu bir hedef se�ip onaylad���nda bunu �a��r�r.
    public void SubmitVote(string targetPlayerID)
    {
        if (!CanActNow)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> S�ra sizde de�ilken oy verilemez.");
            return;
        }

        if (string.IsNullOrEmpty(targetPlayerID))
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> Ge�ersiz hedef, oy g�nderilmedi.");
            return;
        }

        db.VotePlayer(targetPlayerID);

        // Ayn� turda tekrar oy vermeyi engelle (bir sonraki faz de�i�iminde tekrar de�erlendirilecek).
        CanActNow = false;
        OnMyTurnChanged?.Invoke(false);

        Debug.Log($"<color=cyan>[OY G�NDER�LD�]</color> Hedef: {targetPlayerID}");
    }

    // =====================================================
    // UI ER��M YARDIMCILARI
    // =====================================================

    public Room GetCurrentRoom() => currentRoom;

    public Users GetLocalPlayer() => localPlayer;
}
