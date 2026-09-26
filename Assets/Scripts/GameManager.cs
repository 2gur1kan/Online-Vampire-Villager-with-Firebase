using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// ÖNEMLİ MİMARİ KURAL:
// Bu script SADECE odanın HOST'u tarafından çalıştırılmalıdır.
// Host olmayan cihazlarda faz ilerletme, bot oylama ve kazanan hesaplama
// YAPILMAMALI; aksi halde her cihaz aynı anda odayı yönetmeye çalışır ve
// veriler çatışır. Host olmayan cihazlar sadece PlayerTurnController
// üzerinden odayı dinler ve sırası geldiğinde oy gönderir.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Test Ayarları")]
    [Tooltip("Açıksa, oyun sahneye başlarken kendi test odasını kurar. " +
             "ConnectPanel üzerinden gerçek akışı test ederken bunu kapatın.")]
    [SerializeField] private bool autoStartTestGameOnPlay = false;

    private static readonly List<string> SystemBotNames = new List<string>
    {
        "Ali", "Mehmet", "Ayşe", "Fatma", "Mustafa", "Gürkan"
    };

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

    // Firebase'den bir değişiklik geldiğinde çalışır. Artık manuel GetRoomAsync
    // döngüsüne gerek yok; currentRoom her zaman burada güncel tutulur.
    private void OnRoomDataChanged(Room room)
    {
        currentRoom = room;
    }

    // =====================================================
    // PANEL GİRİŞ NOKTASI (ConnectPanel.CreateRoomBTN buradan çağırır)
    // =====================================================
    // Oda zaten oluşturulmuş ve host bu odaya JoinRoom ile katılmış olmalı.
    // Bu metot sadece sistem botlarını odaya ekler ve oyunu başlatır.

    public void HostGameWithSystemBots()
    {
        isHost = true;
        StartCoroutine(HostGameWithSystemBotsRoutine());
    }

    private IEnumerator HostGameWithSystemBotsRoutine()
    {
        db.StartListeningRoom();
        db.ClearStatusMessages();

        Task botsTask = SpawnSystemBotsAsync();
        yield return new WaitUntil(() => botsTask.IsCompleted);

        yield return StartCoroutine(BeginHostedGame());
    }

    private async Task SpawnSystemBotsAsync()
    {
        foreach (string name in SystemBotNames)
        {
            Users bot = new Users
            {
                UserID = Guid.NewGuid().ToString(),
                UserName = name,
                Role = RoleType.Villager,
                IsAlive = true,
                VoteCount = 0,
                IsHost = false,
                IsBot = true
            };

            db.CreatePlayerInRoom(bot);
            await Task.Delay(200);
        }

        Debug.Log("Sistem botları odaya eklendi.");
    }

    // =====================================================
    // TEST AKIŞI (UI OLMADAN SOLO TEST İÇİN)
    // =====================================================

    private IEnumerator StartTestGameRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        Debug.Log("--- OYUN BAŞLATILIYOR (FIREBASE OTOMATİK TEST) ---");

        isHost = true; // Bu cihaz test odasını oluşturduğu için host'tur.

        CreateTestRoom();
        db.StartListeningRoom();

        Task botsTask = SpawnSystemBotsAsync();
        yield return new WaitUntil(() => botsTask.IsCompleted);

        yield return StartCoroutine(BeginHostedGame());
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

    // =====================================================
    // HOST OYUN BAŞLATMA
    // =====================================================

    public IEnumerator BeginHostedGame()
    {
        if (!isHost)
        {
            Debug.LogWarning("<color=yellow>[UYARI]</color> BeginHostedGame sadece host tarafından çağrılmalıdır.");
            yield break;
        }

        Task assignRolesTask = AssignRoles();
        yield return new WaitUntil(() => assignRolesTask.IsCompleted);

        StartCoroutine(GameLoop());
    }

    private async Task AssignRoles()
    {
        // Rolleri dağıtmadan önce tek seferlik kesin bir veri çekişi (döngü değil, tek çağrı).
        currentRoom = await db.GetRoomAsync();

        List<Users> players = currentRoom?.Players;

        if (players == null || players.Count == 0)
        {
            Debug.LogError("Oyuncu listesi boş, roller dağıtılamadı!");
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

        Debug.Log("--- FIREBASE OYUNCU ROLLERİ ---");
        PrintPlayers();
    }

    // =====================================================
    // OYUN AKIŞ DÖNGÜSÜ (GAME LOOP)
    // =====================================================

    private IEnumerator GameLoop()
    {
        while (true)
        {
            // currentRoom, OnRoomDataChanged aracılığıyla zaten güncel; ekstra çekiş yok.
            if (CheckGameOver()) break;

            // --- GECE FAZI BAŞLANGICI ---
            db.ResetRoomVotes();
            yield return new WaitForSeconds(1f);

            // 1. Rolleri Göster
            yield return StartCoroutine(RoleReveal());

            // 2. Gece: Vampir Fazı
            db.ResetRoomVotes();
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(VampireVotePhase());

            // 3. Gece: Doktor Fazı
            db.ResetRoomVotes();
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(DoctorVotePhase());

            // 4. Gündüz Fazı (Ölümler işlenir)
            yield return StartCoroutine(DayPhase());

            if (CheckGameOver()) break;

            // --- GÜNDÜZ FAZI (KÖY OYLAMASI) ---
            db.ResetRoomVotes();
            yield return new WaitForSeconds(1f);

            // 5. Gündüz: Köy Oylaması
            yield return StartCoroutine(VotingPhase());

            // 6. Oylama Sonuçları (İdam)
            yield return StartCoroutine(ResultPhase());
        }

        Debug.Log("<color=cyan>[OYUN BİTTİ] Kazanan Taraf Belirlendi!</color>");

        // Yerel bir C# event yerine Firebase'e yazıyoruz: OnGameEnded gibi bir
        // event SADECE bu (host) cihazda tetiklenirdi, misafir cihazlar hiç
        // haberdar olmazdı. GameState.EndGame ise herkesin zaten dinlediği
        // OnRoomChanged/OnPhaseChanged üzerinden TÜM cihazlara ulaşır.
        db.ChangeGameState(GameState.EndGame);
        isHost = false;
    }

    // =====================================================
    // FAZ COROUTINE'LERİ
    // =====================================================

    private IEnumerator RoleReveal()
    {
        Debug.Log("[FAZ] Gece çöktü / Roller hatırlatılıyor...");
        db.ChangeGameState(GameState.RoleReveal);
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator VampireVotePhase()
    {
        Debug.Log("[FAZ] Gece: Vampirler oy veriyor...");
        db.ChangeGameState(GameState.VampireVote);

        // State değişikliğinin dinleyiciye ulaşması için kısa bir yayılım payı.
        yield return new WaitForSeconds(0.3f);

        var aliveVampires = currentRoom.Players.Where(p => p.Role == RoleType.Vampire && p.IsAlive).ToList();

        if (aliveVampires.Count == 0)
        {
            Debug.Log("<color=yellow>[BİLGİ] Hayatta vampir kalmadığı için vampir fazı atlanıyor.</color>");
            db.SetSelectedDeadPlayer("");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Bot oy kullanır (sadece hayatta olan vampirler, hayatta olan hedeflere)
        BotVoteFirebase(RoleType.Vampire);

        // Gerçek oyuncular kendi cihazlarında PlayerTurnController.SubmitVote çağıracak;
        // GameManager sadece hayattaki VAMPİR olan ve BOT OLMAYAN oyuncuların hepsi
        // bu fazda oy verene (ya da "Geç" ile işaretlenene) kadar bekler. Botların
        // oy vermesi bu bekleyişi asla tek başına bitirmez. Süre aşımı sadece
        // gerçek oyuncu hiç aksiyon almazsa oyunun kilitlenmemesi için bir güvenlik ağıdır.
        float timeOut = 30f;
        float timer = 0f;

        while (timer < timeOut)
        {
            var freshAliveVampires = currentRoom.Players.Where(p => p.Role == RoleType.Vampire && p.IsAlive).ToList();
            bool allRealVotersDone = freshAliveVampires.Where(p => !p.IsBot).All(p => p.HasVotedThisPhase);

            if (allRealVotersDone) break;

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Users target = GetMostVotedPlayer();

        if (target != null)
        {
            db.SetSelectedDeadPlayer(target.UserID);
            Debug.Log($"<color=red>[VAMPİR SEÇİMİ]</color> {target.UserName} kurban olarak seçildi.");
        }
        else
        {
            db.SetSelectedDeadPlayer("");
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator DoctorVotePhase()
    {
        Debug.Log("[FAZ] Gece: Doktor seçim yapıyor...");
        db.ChangeGameState(GameState.DoctorVote);

        yield return new WaitForSeconds(0.3f);

        var aliveDoctors = currentRoom.Players.Where(p => p.Role == RoleType.Doctor && p.IsAlive).ToList();

        if (aliveDoctors.Count == 0)
        {
            Debug.Log("<color=yellow>[BİLGİ] Hayatta doktor kalmadığı için doktor fazı atlanıyor.</color>");
            db.SetOldDoctorVote("");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Bot doktor oy kullanır
        BotVoteFirebase(RoleType.Doctor);

        float timeOut = 30f;
        float timer = 0f;

        while (timer < timeOut)
        {
            var freshAliveDoctors = currentRoom.Players.Where(p => p.Role == RoleType.Doctor && p.IsAlive).ToList();
            bool allRealVotersDone = freshAliveDoctors.Where(p => !p.IsBot).All(p => p.HasVotedThisPhase);

            if (allRealVotersDone) break;

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Users savedPlayer = GetMostVotedPlayer();

        if (savedPlayer != null)
        {
            db.SetOldDoctorVote(savedPlayer.UserID);
            Debug.Log($"<color=green>[DOKTOR KORUMASI]</color> {savedPlayer.UserName} bu gece koruma altına alındı.");
        }
        else
        {
            db.SetOldDoctorVote("");
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator DayPhase()
    {
        Debug.Log("[FAZ] Gündüz oldu! Gece raporu işleniyor...");
        db.ChangeGameState(GameState.Day);

        yield return new WaitForSeconds(0.3f);

        Task applyTask = ApplyDeathsAsync();
        yield return new WaitUntil(() => applyTask.IsCompleted);

        // Yazdığımız değişiklik dinleyiciye ulaşsın diye kısa bir bekleme.
        yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(3f);
    }

    private IEnumerator VotingPhase()
    {
        Debug.Log("[FAZ] Gündüz: Köy oylaması başladı...");
        db.ChangeGameState(GameState.Voting);

        yield return new WaitForSeconds(0.3f);

        // Sadece hayatta olan oyuncular oy kullanır
        BotVoteAllFirebase();

        float timeOut = 30f;
        float timer = 0f;

        while (timer < timeOut)
        {
            var freshAlivePlayers = currentRoom.Players.Where(p => p.IsAlive).ToList();
            bool allRealVotersDone = freshAlivePlayers.Where(p => !p.IsBot).All(p => p.HasVotedThisPhase);

            if (allRealVotersDone) break;

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator ResultPhase()
    {
        Debug.Log("[FAZ] Oylama sonuçları açıklanıyor...");
        db.ChangeGameState(GameState.Result);

        yield return new WaitForSeconds(0.3f);

        Users executedPlayer = GetMostVotedPlayer();

        if (executedPlayer != null)
        {
            db.SetPlayerAlive(executedPlayer.UserID, false);

            string executionMessage = $"{executedPlayer.UserName} köy kararıyla idam edildi!";
            Debug.Log($"<color=orange>[İDAM]</color> {executionMessage}");
            db.SetLastVoteMessage(executionMessage);
        }
        else
        {
            string noExecutionMessage = "Oylamada eşitlik oldu veya kimse seçilmedi. İdam yapılmadı.";
            Debug.Log($"[BİLGİ] {noExecutionMessage}");
            db.SetLastVoteMessage(noExecutionMessage);
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
                string savedMessage = "Doktor kurbanı kurtardı! Kimse ölmedi.";
                Debug.Log($"<color=green>[GECE SONUCU]</color> {savedMessage}");
                db.SetLastNightMessage(savedMessage);
            }
            else
            {
                db.SetPlayerAlive(deadID, false);
                Users victim = currentRoom.Players.FirstOrDefault(p => p.UserID == deadID);

                string deathMessage = $"{victim?.UserName} gece saldırıya uğrayarak öldü.";
                Debug.Log($"<color=red>[ÖLÜM]</color> {deathMessage}");
                db.SetLastNightMessage(deathMessage);

                await Task.Delay(300);
            }
        }
        else
        {
            string noAttackMessage = "Gece kimse saldırıya uğramadı.";
            Debug.Log($"[GECE SONUCU] {noAttackMessage}");
            db.SetLastNightMessage(noAttackMessage);
        }
    }

    private bool CheckGameOver()
    {
        if (currentRoom == null || currentRoom.Players == null) return false;

        int aliveVampires = currentRoom.Players.Count(p => p.Role == RoleType.Vampire && p.IsAlive);
        int aliveInnocents = currentRoom.Players.Count(p => p.Role != RoleType.Vampire && p.IsAlive);

        Debug.Log($"[DURUM KONTROLÜ] Hayatta vampir: {aliveVampires} | Hayatta masum: {aliveInnocents}");

        if (aliveVampires == 0)
        {
            string winMessage = "KÖYLÜLER KAZANDI!";
            Debug.Log($"<color=green>===============================\n{winMessage}\n===============================</color>");
            db.SetGameOverMessage(winMessage);
            return true;
        }

        if (aliveVampires >= aliveInnocents)
        {
            string winMessage = "VAMPİRLER KAZANDI!";
            Debug.Log($"<color=red>===============================\n{winMessage}\n===============================</color>");
            db.SetGameOverMessage(winMessage);
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

        // Eşitlik kontrolü (en çok oyu alan 2 kişi eşit oy aldıysa kimse seçilmez)
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
    // BOT SİMÜLASYON METODLARI (SADECE TEST İÇİN)
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
            Debug.Log($"[BOT OY] ({voter.Role}) {voter.UserName} -> {target.UserName} kişisine oy verdi.");
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
            Debug.Log($"[BOT KÖY OYU] {p.UserName} -> {target.UserName} kişisine oy verdi.");
        }
    }
}
