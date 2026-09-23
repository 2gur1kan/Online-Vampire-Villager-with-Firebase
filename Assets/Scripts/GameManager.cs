using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// �NEML� M�MAR� KURAL:
// Bu script SADECE odan�n HOST'u taraf�ndan �al��t�r�lmal�d�r.
// Host olmayan cihazlarda faz ilerletme, bot oylama ve kazanan hesaplama YAP�LMAMALI;
// aksi halde her cihaz ayn� anda oday� y�netmeye �al���r ve veriler �at���r.
// Host olmayan cihazlar sadece PlayerTurnController �zerinden oday� dinler ve s�ras� geldi�inde oy g�nderir.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Test Ayarlar�")]
    [SerializeField] private bool autoStartTestGameOnPlay = true;

    private FireBaseDataBase db;
    private Room currentRoom;
    private bool isHost;

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

        if (autoStartTestGameOnPlay)
        {
            StartCoroutine(StartTestGameRoutine());
        }
    }

    private void OnDestroy()
    {
        if (db != null)
        {
            db.OnRoomChanged -= OnRoomDataChanged;
        }
    }

    // Firebase'den bir de�i�iklik geldi�inde �al���r. Art�k manuel GetRoomAsync d�ng�s�ne gerek yok;
    // currentRoom her zaman burada g�ncel tutulur.
    private void OnRoomDataChanged(Room room)
    {
        currentRoom = room;
    }

    // =====================================================
    // TEST AK1I (SOLO TEST ���N HOST BOOTSTRAP)
    // =====================================================
    // Ger�ek lobi ak���nda bunun yerine: host "Oyunu Ba�lat" butonuna bast���nda
    // BeginHostedGame() �a�r�l�r (oda ve ger�ek oyuncular zaten lobide olu�turulmu� olur).

    private IEnumerator StartTestGameRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        Debug.Log("--- OYUN BA�LATILIYOR (F�REBASE OTOMAT�K TEST) ---");

        isHost = true; // Bu cihaz test odas�n� olu�turdu�u i�in host'tur.

        CreateTestRoom();
        db.StartListeningRoom();

        Task testPlayersTask = TestPlayersAsync();
        yield return new WaitUntil(() => testPlayersTask.IsCompleted);

        StartCoroutine(BeginHostedGame());
    }

    private void CreateTestRoom()
    {
        Room room = new Room
        {
            RoomID = "ROOM_TEST_1",
            HostID = db.CurrentPlayerID,
            State = GameState.Lobby,
            TotalVote = 0,
            SelectedDeadPlayer = "",
            OldDoctorVote = ""
        };

        db.CreateRoom(room);
    }

    private async Task TestPlayersAsync()
    {
        List<string> testNames = new List<string> { "Ali", "Mehmet", "Ay�e", "Fatma", "Mustafa", "Gurkan" };

        foreach (string name in testNames)
        {
            Users p = new Users
            {
                UserID = Guid.NewGuid().ToString(),
                UserName = name,
                Role = RoleType.Villager,
                IsAlive = true,
                VoteCount = 0,
                IsHost = false
            };

            db.JoinRoom("ROOM_TEST_1", p);
            await Task.Delay(200);
        }

        Debug.Log("T�m test oyuncular� Firebase'e eklendi.");
    }

    // =====================================================
    // HOST OYUN BA�LATMA
    // =====================================================

    public IEnumerator BeginHostedGame()
    {
        if (!isHost)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> BeginHostedGame sadece host taraf�ndan �a�r�lmal�d�r.");
            yield break;
        }

        Task assignRolesTask = AssignRoles();
        yield return new WaitUntil(() => assignRolesTask.IsCompleted);

        StartCoroutine(GameLoop());
    }

    private async Task AssignRoles()
    {
        // Rolleri da��tmadan �nce tek seferlik kesin bir veri �eki�i (d�ng� de�il, tek ��a��r�).
        currentRoom = await db.GetRoomAsync();

        List<Users> players = currentRoom?.Players;

        if (players == null || players.Count == 0)
        {
            Debug.LogError("Oyuncu listesi bo�, roller da��t�lamad�!");
            return;
        }

        List<RoleType> roles = new List<RoleType>
        {
            RoleType.Vampire,
            RoleType.Doctor
        };

        while (roles.Count < players.Count)
        {
            roles.Add(RoleType.Villager);
        }

        System.Random rng = new System.Random();
        roles = roles.OrderBy(x => rng.Next()).ToList();

        for (int i = 0; i < players.Count; i++)
        {
            db.SetPlayerRole(players[i].UserID, roles[i]);
        }

        await Task.Delay(500);
        currentRoom = await db.GetRoomAsync();

        Debug.Log("--- F�REBASE OYUNCU ROLLER� ---");
        PrintPlayers();
    }

    // =====================================================
    // OYUN AKI� D�NG�S� (GAME LOOP)
    // =====================================================

    private IEnumerator GameLoop()
    {
        while (true)
        {
            // currentRoom, OnRoomDataChanged arac�l���yla zaten g�ncel; ekstra �eki� yok.
            if (CheckGameOver()) break;

            // --- GECE FAZI BA�LANGICI ---
            db.ResetRoomVotes();
            yield return new WaitForSeconds(1f);

            // 1. Rolleri G�ster
            yield return StartCoroutine(RoleReveal());

            // 2. Gece: Vampir Faz�
            db.ResetRoomVotes();
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(VampireVotePhase());

            // 3. Gece: Doktor Faz�
            db.ResetRoomVotes();
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(DoctorVotePhase());

            // 4. G�nd�z Faz� (�l�mler ��lenir)
            yield return StartCoroutine(DayPhase());

            if (CheckGameOver()) break;

            // --- G�ND�Z FAZI (K�Y OYLAMASI) ---
            db.ResetRoomVotes();
            yield return new WaitForSeconds(1f);

            // 5. G�nd�z: K�y Oylamas�
            yield return StartCoroutine(VotingPhase());

            // 6. Oylama Sonu�lar� (�dam)
            yield return StartCoroutine(ResultPhase());
        }

        Debug.Log("<color=cyan>[OYUN B�TT�] Kazanan Taraf Belirlendi!</color>");
    }

    // =====================================================
    // FAZ COROUTINE'LER�
    // =====================================================

    private IEnumerator RoleReveal()
    {
        Debug.Log("[FAZ] Gece ��kt� / Roller Hat�rlat�l�yor...");
        db.ChangeGameState(GameState.RoleReveal);
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator VampireVotePhase()
    {
        Debug.Log("[FAZ] Gece: Vampirler Oy Veriyor...");
        db.ChangeGameState(GameState.VampireVote);

        // State de�i�ikli�inin dinleyiciye ula�mas� i�in k�sa bir yay�l�m payı.
        yield return new WaitForSeconds(0.3f);

        var aliveVampires = currentRoom.Players.Where(p => p.Role == RoleType.Vampire && p.IsAlive).ToList();

        if (aliveVampires.Count == 0)
        {
            Debug.Log("<color=yellow>[B�LG�] Hayatta Vampir kalmad��� i�in Vampir Faz� atlan�yor.</color>");
            db.SetSelectedDeadPlayer("");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Bot Oy Kullan�r (Sadece hayatta olan vampirler, hayatta olan hedeflere)
        BotVoteFirebase(RoleType.Vampire);

        // Ger�ek oyuncular kendi cihazlar�nda PlayerTurnController.SubmitVote �a��racak;
        // burada sadece oylar�n toplanmas�n� bekliyoruz (polling de�il, cache kontrol�).
        float timeOut = 5f;
        float timer = 0f;

        while (timer < timeOut)
        {
            if (currentRoom.TotalVote >= aliveVampires.Count) break;
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Users target = GetMostVotedPlayer();

        if (target != null)
        {
            db.SetSelectedDeadPlayer(target.UserID);
            Debug.Log($"<color=red>[VAMP�R SE��M�]</color> {target.UserName} kurban olarak se�ildi.");
        }
        else
        {
            db.SetSelectedDeadPlayer("");
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator DoctorVotePhase()
    {
        Debug.Log("[FAZ] Gece: Doktor Se�im Yap�yor...");
        db.ChangeGameState(GameState.DoctorVote);

        yield return new WaitForSeconds(0.3f);

        var aliveDoctors = currentRoom.Players.Where(p => p.Role == RoleType.Doctor && p.IsAlive).ToList();

        if (aliveDoctors.Count == 0)
        {
            Debug.Log("<color=yellow>[B�LG�] Hayatta Doktor kalmad��� i�in Doktor Faz� atlan�yor.</color>");
            db.SetOldDoctorVote("");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Bot Doktor Oy Kullan�r
        BotVoteFirebase(RoleType.Doctor);

        float timeOut = 5f;
        float timer = 0f;

        while (timer < timeOut)
        {
            if (currentRoom.TotalVote >= aliveDoctors.Count) break;
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Users savedPlayer = GetMostVotedPlayer();

        if (savedPlayer != null)
        {
            db.SetOldDoctorVote(savedPlayer.UserID);
            Debug.Log($"<color=green>[DOKTOR KORUMASI]</color> {savedPlayer.UserName} bu gece koruma alt�na al�nd�.");
        }
        else
        {
            db.SetOldDoctorVote("");
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator DayPhase()
    {
        Debug.Log("[FAZ] G�nd�z Oldu! Gece Raporu ��leniyor...");
        db.ChangeGameState(GameState.Day);

        yield return new WaitForSeconds(0.3f);

        Task applyTask = ApplyDeathsAsync();
        yield return new WaitUntil(() => applyTask.IsCompleted);

        // Yazd���m�z de�i�iklik dinleyiciye ula�s�n diye k�sa bir bekleme.
        yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(3f);
    }

    private IEnumerator VotingPhase()
    {
        Debug.Log("[FAZ] G�nd�z: K�y Oylamas� Ba�lad�...");
        db.ChangeGameState(GameState.Voting);

        yield return new WaitForSeconds(0.3f);

        // SADECE HAYATTA OLAN OYUNCULAR OY KULLANIR
        BotVoteAllFirebase();

        int aliveCount = currentRoom.Players.Count(p => p.IsAlive);

        float timeOut = 7f;
        float timer = 0f;

        while (timer < timeOut)
        {
            if (currentRoom.TotalVote >= aliveCount) break;
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator ResultPhase()
    {
        Debug.Log("[FAZ] Oylama Sonu�lar� A��klan�yor...");
        db.ChangeGameState(GameState.Result);

        yield return new WaitForSeconds(0.3f);

        Users executedPlayer = GetMostVotedPlayer();

        if (executedPlayer != null)
        {
            db.SetPlayerAlive(executedPlayer.UserID, false);
            Debug.Log($"<color=orange>[�DAM]</color> {executedPlayer.UserName} k�y karar�yla idam edildi!");
        }
        else
        {
            Debug.Log("[B�LG�] Oylamada e�itlik oldu veya kimse se�ilmedi. �dam yap�lmad�.");
        }

        yield return new WaitForSeconds(3f);
    }

    // =====================================================
    // YARDIMCI MANTIK VE KURALLAR
    // =====================================================

    private async Task ApplyDeathsAsync()
    {
        string deadID = currentRoom.SelectedDeadPlayer;
        string doctorID = currentRoom.OldDoctorVote;

        if (!string.IsNullOrEmpty(deadID))
        {
            if (deadID == doctorID)
            {
                Debug.Log("<color=green>[GECE SONUCU]</color> Doktor kurban� kurtard�! Kimse �lmedi.");
            }
            else
            {
                db.SetPlayerAlive(deadID, false);
                Users victim = currentRoom.Players.FirstOrDefault(p => p.UserID == deadID);
                Debug.Log($"<color=red>[�L�M]</color> {victim?.UserName} gece sald�r�ya u�rayarak �ld�.");
                await Task.Delay(300);
            }
        }
        else
        {
            Debug.Log("[GECE SONUCU] Gece kimse sald�r�ya u�ramad�.");
        }
    }

    private bool CheckGameOver()
    {
        if (currentRoom == null || currentRoom.Players == null) return false;

        int aliveVampires = currentRoom.Players.Count(p => p.Role == RoleType.Vampire && p.IsAlive);
        int aliveInnocents = currentRoom.Players.Count(p => p.Role != RoleType.Vampire && p.IsAlive);

        Debug.Log($"[DURUM KONTROL�] Hayatta Vampir: {aliveVampires} | Hayatta Masum: {aliveInnocents}");

        if (aliveVampires == 0)
        {
            Debug.Log("<color=green>===============================\nK�YL�LER KAZANDI!\n===============================</color>");
            return true;
        }

        if (aliveVampires >= aliveInnocents)
        {
            Debug.Log("<color=red>===============================\nVAMP�RLER KAZANDI!\n===============================</color>");
            return true;
        }

        return false;
    }

    private Users GetMostVotedPlayer()
    {
        if (currentRoom == null || currentRoom.Players == null) return null;

        var candidates = currentRoom.Players
            .Where(p => p.IsAlive && p.VoteCount > 0)
            .OrderByDescending(p => p.VoteCount)
            .ToList();

        if (candidates.Count == 0) return null;

        // E�itlik kontrol� (En �ok oyu alan 2 ki�i e�it oy ald�ysa kimse se�ilmez)
        if (candidates.Count > 1 && candidates[0].VoteCount == candidates[1].VoteCount)
        {
            return null;
        }

        return candidates.First();
    }

    private void PrintPlayers()
    {
        foreach (var p in currentRoom.Players)
        {
            Debug.Log($"{p.UserName} | Rol: {p.Role} | Hayatta: {p.IsAlive}");
        }
        Debug.Log("-------------------------------");
    }

    // =====================================================
    // BOT S�M�LASYON METODLARI (SADECE TEST ���N)
    // =====================================================

    private void BotVoteFirebase(RoleType role)
    {
        var voters = currentRoom.Players.Where(p => p.Role == role && p.IsAlive).ToList();

        if (voters.Count == 0) return;

        foreach (var voter in voters)
        {
            List<Users> validTargets;

            if (role == RoleType.Vampire)
            {
                validTargets = currentRoom.Players
                    .Where(p => p.IsAlive && p.UserID != voter.UserID)
                    .ToList();
            }
            else
            {
                validTargets = currentRoom.Players
                    .Where(p => p.IsAlive)
                    .ToList();
            }

            if (validTargets.Count == 0) continue;

            Users target = validTargets[UnityEngine.Random.Range(0, validTargets.Count)];
            db.VotePlayer(target.UserID);
            Debug.Log($"[BOT OY] ({voter.Role}) {voter.UserName} -> {target.UserName} ki�isine oy verdi.");
        }
    }

    private void BotVoteAllFirebase()
    {
        var alivePlayers = currentRoom.Players.Where(p => p.IsAlive).ToList();

        if (alivePlayers.Count == 0) return;

        foreach (var p in alivePlayers)
        {
            Users target = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];
            db.VotePlayer(target.UserID);
            Debug.Log($"[BOT K�Y OYU] {p.UserName} -> {target.UserName} ki�isine oy verdi.");
        }
    }
}
