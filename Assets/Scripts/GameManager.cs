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

    // =====================================================
    // SÜRE AYARLARI (HEPSİ INSPECTOR'DAN AYARLANABİLİR)
    // =====================================================

    [Header("Faz Süreleri (VotePanel'in Senkron Geri Sayımı)")]
    [Tooltip("Rol tanıtım ekranının (RoleRevealPanel) kaç saniye görüneceği.")]
    [SerializeField] private float roleRevealDurationSeconds = 3f;

    [Tooltip("Vampir oylamasının süresi. VotePanel bu kadar geri sayar.")]
    [SerializeField] private float vampireVoteDurationSeconds = 10f;

    [Tooltip("Doktor oylamasının süresi. VotePanel bu kadar geri sayar.")]
    [SerializeField] private float doctorVoteDurationSeconds = 10f;

    [Tooltip("Köy oylamasının süresi. VotePanel bu kadar geri sayar.")]
    [SerializeField] private float villageVoteDurationSeconds = 30f;

    [Tooltip("Gerçek oyuncu hiç aksiyon almazsa, yukarıdaki sürelere ek olarak " +
             "GameManager'ın (oyunun kilitlenmemesi için) bekleyeceği güvenlik payı.")]
    [SerializeField] private float waitSafetyBufferSeconds = 5f;

    [Header("Sıfırlama Sonrası Bekleme Süreleri")]
    [Tooltip("Oy sıfırlama tamamlandıktan sonra, Rol Tanıtımı fazına geçmeden önceki bekleme.")]
    [SerializeField] private float preRoleRevealWaitSeconds = 1f;

    [Tooltip("Oy sıfırlama tamamlandıktan sonra, Vampir fazına geçmeden önceki bekleme.")]
    [SerializeField] private float preVampireVoteWaitSeconds = 0.5f;

    [Tooltip("Oy sıfırlama tamamlandıktan sonra, Doktor fazına geçmeden önceki bekleme.")]
    [SerializeField] private float preDoctorVoteWaitSeconds = 0.5f;

    [Tooltip("Oy sıfırlama tamamlandıktan sonra, Köy Oylaması fazına geçmeden önceki bekleme.")]
    [SerializeField] private float preVillageVoteWaitSeconds = 1f;

    [Header("Firebase Yayılma Payları")]
    [Tooltip("ChangeGameState çağrısından sonra, yeni state'in tüm cihazlara ulaşması " +
             "için her fazın başında bırakılan kısa bekleme.")]
    [SerializeField] private float statePropagationDelaySeconds = 0.3f;

    [Tooltip("Hiç vampir/doktor kalmadığında, o fazın atlandığını gösteren kısa bekleme.")]
    [SerializeField] private float noActorSkipWaitSeconds = 1f;

    [Tooltip("Oy sayımını okumadan önce, son oy işleminin (transaction) Firebase'e " +
             "tam olarak yansıması için bırakılan güvenlik payı.")]
    [SerializeField] private float voteResultBufferSeconds = 1f;

    [Header("Sonuç Ekranlarının Görünme Süresi")]
    [Tooltip("Vampir fazı sonucu (kurban seçildi/seçilmedi) belirlendikten sonra bu ekranın açık kalma süresi.")]
    [SerializeField] private float vampirePhaseResultDisplaySeconds = 1.5f;

    [Tooltip("Doktor fazı sonucu (koruma kararı) belirlendikten sonra bu ekranın açık kalma süresi.")]
    [SerializeField] private float doctorPhaseResultDisplaySeconds = 1.5f;

    [Tooltip("Gece sonucu mesajının (ölüm/kurtarma) Firebase'e yansıması için bırakılan kısa bekleme.")]
    [SerializeField] private float dayResultPropagationDelaySeconds = 0.5f;

    [Tooltip("Gece sonucu mesajının (kim öldü/kurtuldu) ekranda kalma süresi.")]
    [SerializeField] private float dayResultDisplaySeconds = 3f;

    [Tooltip("Köy oylaması bittikten sonra, idam sonucuna geçmeden önceki bekleme.")]
    [SerializeField] private float votingPhaseResultDisplaySeconds = 1.5f;

    [Tooltip("İdam/oylama sonucu mesajının ekranda kalma süresi.")]
    [SerializeField] private float resultPhaseDisplaySeconds = 3f;

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

        // Aynı oda ikinci kez "Oyunu Başlat" ile kullanılıyor olabilir: önce
        // bir önceki oyundan kalan botları temizle, gerçek oyuncuları tekrar
        // hayatta yap, SONRA taze botları ekle. Bu sıra olmadan eski botların
        // üstüne yeni bir set daha eklenir ve ölü oyuncular canlanmaz.
        Task removeBotsTask = db.RemoveAllBotsAsync();
        yield return new WaitUntil(() => removeBotsTask.IsCompleted);

        Task reviveTask = db.ResetAllPlayersAliveAsync();
        yield return new WaitUntil(() => reviveTask.IsCompleted);

        Task botsTask = BOTManager.Instance.SpawnSystemBotsAsync();
        yield return new WaitUntil(() => botsTask.IsCompleted);

        yield return StartCoroutine(BeginHostedGame());
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

        Task testBotsTask = BOTManager.Instance.SpawnSystemBotsAsync();
        yield return new WaitUntil(() => testBotsTask.IsCompleted);

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
            yield return StartCoroutine(ResetVotes());
            yield return StartCoroutine(Wait(preRoleRevealWaitSeconds));

            // 1. Rolleri Göster
            yield return StartCoroutine(RoleReveal());

            // 2. Gece: Vampir Fazı
            yield return StartCoroutine(ResetVotes());
            yield return StartCoroutine(Wait(preVampireVoteWaitSeconds));
            yield return StartCoroutine(VampireVotePhase());

            // 3. Gece: Doktor Fazı
            yield return StartCoroutine(ResetVotes());
            yield return StartCoroutine(Wait(preDoctorVoteWaitSeconds));
            yield return StartCoroutine(DoctorVotePhase());

            // 4. Gündüz Fazı (Ölümler işlenir)
            yield return StartCoroutine(ResetVotes());
            yield return StartCoroutine(DayPhase());

            if (CheckGameOver()) break;

            // --- GÜNDÜZ FAZI (KÖY OYLAMASI) ---
            yield return StartCoroutine(ResetVotes());
            yield return StartCoroutine(Wait(preVillageVoteWaitSeconds));

            // 5. Gündüz: Köy Oylaması
            yield return StartCoroutine(ResetVotes());
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

    // ResetRoomVotesAsync tamamlanmadan (yani sıfırlama Firebase'e gerçekten
    // yazılmadan) bir sonraki faza asla geçmeyi sağlar. Bu olmadan, gecikmeli
    // bir sıfırlama isteği az önce atılan oyların üzerine geç gelip onları
    // sıfırlayabiliyordu ("saldırı oluyor ama kimse ölmüyor" hatası buydu).

    private IEnumerator ResetVotes()
    {
        // 1. Yerel bellekteki oyuncuları anında sıfırla
        if (currentRoom != null && currentRoom.Players != null)
        {
            foreach (var p in currentRoom.Players)
            {
                p.HasVotedThisPhase = false;
                p.VoteCount = 0; // Oy sayılarını da yerelde sıfırlayın
            }
        }

        // 2. Firebase tarafında sıfırlamayı başlat ve bitmesini bekle
        Task resetTask = db.ResetRoomVotesAsync();
        yield return new WaitUntil(() => resetTask.IsCompleted);

        // 3. Firebase event'inin yerel currentRoom'a yansımasını teyit et
        // (En azından bir oyuncunun HasVotedThisPhase değeri false olana dek bekle)
        float timeout = 2f;
        float elapsed = 0f;
        while (elapsed < timeout && currentRoom != null && currentRoom.Players.Any(p => p.HasVotedThisPhase))
        {
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator Wait(float extraWaitSeconds)
    {

        if (extraWaitSeconds > 0f)
        {
            yield return new WaitForSeconds(extraWaitSeconds);
        }
    }

    // =====================================================
    // FAZ COROUTINE'LERİ
    // =====================================================

    private IEnumerator RoleReveal()
    {
        Debug.Log("[FAZ] Gece çöktü / Roller hatırlatılıyor...");
        db.ChangeGameState(GameState.RoleReveal);
        db.SetLastEventMessage("Roller dağıtıldı. Oyun başlıyor...");
        yield return new WaitForSeconds(roleRevealDurationSeconds);
    }

    private IEnumerator VampireVotePhase()
    {
        Debug.Log("[FAZ] Gece: Vampirler oy veriyor...");
        db.ChangeGameStateWithTimer(GameState.VampireVote, vampireVoteDurationSeconds);
        db.SetLastEventMessage("Gece çöktü. Vampirler kurbanını seçiyor...");

        // State değişikliğinin dinleyiciye ulaşması için kısa bir yayılım payı.
        yield return new WaitForSeconds(statePropagationDelaySeconds);

        var aliveVampires = currentRoom.Players.Where(p => p.Role == RoleType.Vampire && p.IsAlive).ToList();

        if (aliveVampires.Count == 0)
        {
            Debug.Log("<color=yellow>[BİLGİ] Hayatta vampir kalmadığı için vampir fazı atlanıyor.</color>");
            db.SetSelectedDeadPlayer("");
            yield return new WaitForSeconds(noActorSkipWaitSeconds);
            yield break;
        }

        // Bot oy kullanır (sadece hayatta olan, IsBot=true olan vampirler, hayatta olan hedeflere)
        BOTManager.Instance.BotVoteFirebase(RoleType.Vampire, currentRoom);

        // Gerçek oyuncular kendi cihazlarında PlayerTurnController.SubmitVote çağıracak;
        // GameManager sadece hayattaki VAMPİR olan ve BOT OLMAYAN oyuncuların hepsi
        // bu fazda oy verene (ya da "Geç" ile işaretlenene) kadar bekler. Botların
        // oy vermesi bu bekleyişi asla tek başına bitirmez. Süre aşımı sadece
        // gerçek oyuncu hiç aksiyon almazsa oyunun kilitlenmemesi için bir güvenlik ağıdır.
        float timeOut = vampireVoteDurationSeconds + waitSafetyBufferSeconds;
        float timer = 0f;

        while (timer < timeOut)
        {
            // ÖNEMLİ: Sadece vampir oy verince faz hemen bitmiyor. Vampir
            // gerçek hedefini seçse bile, DİĞER gerçek oyuncuların da
            // (anonimlik için gösterilen "Geç" butonuna basarak) onay
            // vermesi gerekir — aksi halde süre (10sn) doğal olarak dolar.
            // Böylece dışarıdan bakan biri, kimin gerçekten seçim yaptığını
            // "ekran diğerlerinden önce kapandı" diye de anlayamaz.
            var freshAlivePlayers = currentRoom.Players.Where(p => p.IsAlive).ToList();
            bool allRealVotersDone = freshAlivePlayers.Where(p => !p.IsBot).All(p => p.HasVotedThisPhase);

            if (allRealVotersDone) break;

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        // "HasVotedThisPhase" işaretlenmesi ile VoteCount transaction'ının
        // tamamlanması iki ayrı yazma işlemi; aradaki küçük gecikmeyi
        // (transaction, düz bir yazmadan biraz daha yavaş olabilir) telafi
        // etmek için oy sayımını okumadan önce kısa bir pay bırakıyoruz.
        yield return new WaitForSeconds(voteResultBufferSeconds);

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

        yield return new WaitForSeconds(vampirePhaseResultDisplaySeconds);
    }

    private IEnumerator DoctorVotePhase()
    {
        Debug.Log("[FAZ] Gece: Doktor seçim yapıyor...");
        db.ChangeGameStateWithTimer(GameState.DoctorVote, doctorVoteDurationSeconds);
        db.SetLastEventMessage("Doktor bu gece kimi koruyacağına karar veriyor...");

        yield return new WaitForSeconds(statePropagationDelaySeconds);

        var aliveDoctors = currentRoom.Players.Where(p => p.Role == RoleType.Doctor && p.IsAlive).ToList();

        if (aliveDoctors.Count == 0)
        {
            Debug.Log("<color=yellow>[BİLGİ] Hayatta doktor kalmadığı için doktor fazı atlanıyor.</color>");
            db.SetOldDoctorVote("");
            yield return new WaitForSeconds(noActorSkipWaitSeconds);
            yield break;
        }

        // Bot doktor oy kullanır (sadece IsBot=true olanlar)
        BOTManager.Instance.BotVoteFirebase(RoleType.Doctor, currentRoom);

        float timeOut = doctorVoteDurationSeconds + waitSafetyBufferSeconds;
        float timer = 0f;

        while (timer < timeOut)
        {
            // Aynı mantık: sadece doktor değil, TÜM gerçek oyuncular (Geç
            // dahil) onaylamadan faz erken bitmesin.
            var freshAlivePlayers = currentRoom.Players.Where(p => p.IsAlive).ToList();
            bool allRealVotersDone = freshAlivePlayers.Where(p => !p.IsBot).All(p => p.HasVotedThisPhase);

            if (allRealVotersDone) break;

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(voteResultBufferSeconds);

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

        yield return new WaitForSeconds(doctorPhaseResultDisplaySeconds);
    }

    private IEnumerator DayPhase()
    {
        Debug.Log("[FAZ] Gündüz oldu! Gece raporu işleniyor...");
        db.ChangeGameState(GameState.Day);
        db.SetLastEventMessage("Gün ağarıyor. Gece yaşananlar açıklanıyor...");

        yield return new WaitForSeconds(statePropagationDelaySeconds);

        Task applyTask = ApplyDeathsAsync();
        yield return new WaitUntil(() => applyTask.IsCompleted);

        // Yazdığımız değişiklik dinleyiciye ulaşsın diye kısa bir bekleme.
        yield return new WaitForSeconds(dayResultPropagationDelaySeconds);
        yield return new WaitForSeconds(dayResultDisplaySeconds);
    }

    private IEnumerator VotingPhase()
    {
        Debug.Log("[FAZ] Gündüz: Köy oylaması başladı...");
        db.ChangeGameStateWithTimer(GameState.Voting, villageVoteDurationSeconds);
        db.SetLastEventMessage("Köy oylaması başladı. Şüphelendiğiniz kişiyi seçin...");

        yield return new WaitForSeconds(statePropagationDelaySeconds);

        // ÖNEMLİ: Botlar fazın hemen başında oy VERMEZ. Önce gerçek (bot
        // olmayan) oyunculardan EN AZ birinin oy kullanmasını (ya da
        // çekimser/"Geç" ile işaretlenmesini) bekleriz; böylece botlar
        // oyuncunun önüne geçip akışı kendileri belirleyemez, oyuncuyu
        // beklemek zorunda kalırlar.
        yield return StartCoroutine(WaitForAnyRealVoteThenBotsVote());

        // ÖNEMLİ: Sabit bir "timeOut" YERİNE, her turda currentRoom'daki
        // GÜNCEL PhaseDurationSeconds'ı okuyoruz. Host, VotePanel'deki
        // "+20sn" butonuna basarsa bu değer Firebase'de büyür; bu bekleme de
        // otomatik olarak uzar. Sabit tutsaydık, host süreyi uzatsa bile
        // GameManager eski (kısa) süreyle fazı erken bitirip VotePanel hâlâ
        // ekstra süre gösterirken oylamayı kapatabilirdi.
        while (true)
        {
            var freshAlivePlayers = currentRoom.Players.Where(p => p.IsAlive).ToList();
            bool allRealVotersDone = freshAlivePlayers.Where(p => !p.IsBot).All(p => p.HasVotedThisPhase);

            if (allRealVotersDone) break;

            double elapsedSeconds = (db.GetServerNowMillis() - currentRoom.PhaseStartTimeMillis) / 1000.0;
            float effectiveTimeOut = currentRoom.PhaseDurationSeconds + waitSafetyBufferSeconds;

            if (elapsedSeconds >= effectiveTimeOut) break;

            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(votingPhaseResultDisplaySeconds);
    }

    // Köy oylaması fazında, botlar oy kullanmadan önce gerçek (bot olmayan)
    // hayattaki oyunculardan EN AZ birinin kendi oyunu kullanmasını (ya da
    // "Geç" ile işaretlenmesini) bekler. Hiç gerçek oyuncu kalmadıysa
    // (tamamen bot testi gibi) sonsuza kadar beklememesi için villageVoteDurationSeconds
    // + waitSafetyBufferSeconds sonunda yine de oy kullanmaya başlar.
    private IEnumerator WaitForAnyRealVoteThenBotsVote()
    {
        Debug.Log("<color=magenta>[BOT BEKLEME]</color> Köy oylaması başladı. Gerçek oyuncuların oyu bekleniyor...");

        float elapsedTime = 0f;
        float maxWaitTime = 15f;

        while (elapsedTime < maxWaitTime)
        {
            // Class seviyesindeki currentRoom değişkeninin null olmadığını kontrol et
            if (currentRoom != null && currentRoom.Players != null)
            {
                // 1. Hayattaki gerçek oyuncuları al
                var realAlivePlayers = currentRoom.Players.Where(p => p.IsAlive && !p.IsBot).ToList();

                // 2. Eğer hayatta hiç gerçek oyuncu yoksa boşuna bekleme
                if (realAlivePlayers.Count == 0)
                {
                    Debug.Log("<color=magenta>[BOT BEKLEME]</color> Hayatta gerçek oyuncu yok, botlar oy kullanıyor.");
                    BOTManager.Instance.BotVoteAllFirebase(currentRoom);
                    yield break;
                }

                // 3. Oy kullanan gerçek oyuncu var mı?
                if (realAlivePlayers.Any(p => p.HasVotedThisPhase))
                {
                    Debug.Log($"<color=magenta>[BOT BEKLEME]</color> Gerçek oyuncu oy kullandı, botlar oy kullanıyor.");
                    BOTManager.Instance.BotVoteAllFirebase(currentRoom);
                    yield break;
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Debug.Log("<color=magenta>[BOT BEKLEME]</color> Süre doldu, botlar oy kullanıyor.");
        BOTManager.Instance.BotVoteAllFirebase(currentRoom);
    }

    private IEnumerator ResultPhase()
    {
        Debug.Log("[FAZ] Oylama sonuçları açıklanıyor...");
        db.ChangeGameState(GameState.Result);
        db.SetLastEventMessage("Oylama sonuçları açıklanıyor...");

        yield return new WaitForSeconds(statePropagationDelaySeconds);

        Users executedPlayer = GetMostVotedPlayer();

        if (executedPlayer != null)
        {
            db.SetPlayerAlive(executedPlayer.UserID, false);

            string executionMessage = $"{executedPlayer.UserName} köy kararıyla idam edildi!";
            Debug.Log($"<color=orange>[İDAM]</color> {executionMessage}");
            db.SetLastEventMessage(executionMessage);
        }
        else
        {
            string noExecutionMessage = "Oylamada eşitlik oldu veya kimse seçilmedi. İdam yapılmadı.";
            Debug.Log($"[BİLGİ] {noExecutionMessage}");
            db.SetLastEventMessage(noExecutionMessage);
        }

        yield return new WaitForSeconds(resultPhaseDisplaySeconds);
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
                db.SetLastEventMessage(savedMessage);
            }
            else
            {
                db.SetPlayerAlive(deadID, false);
                Users victim = currentRoom.Players.FirstOrDefault(p => p.UserID == deadID);

                string deathMessage = $"{victim?.UserName} gece saldırıya uğrayarak öldü.";
                Debug.Log($"<color=red>[ÖLÜM]</color> {deathMessage}");
                db.SetLastEventMessage(deathMessage);

                await Task.Delay(300);
            }
        }
        else
        {
            string noAttackMessage = "Gece kimse saldırıya uğramadı.";
            Debug.Log($"[GECE SONUCU] {noAttackMessage}");
            db.SetLastEventMessage(noAttackMessage);
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
            db.SetLastEventMessage(winMessage);
            return true;
        }

        if (aliveVampires >= aliveInnocents)
        {
            string winMessage = "VAMPİRLER KAZANDI!";
            Debug.Log($"<color=red>===============================\n{winMessage}\n===============================</color>");
            db.SetLastEventMessage(winMessage);
            return true;
        }

        return false;
    }

    private Users GetMostVotedPlayer()
    {
        if (currentRoom == null || currentRoom.Players == null) return null;

        string voteDump = string.Join(", ", currentRoom.Players.Select(p => $"{p.UserName}:{p.VoteCount}"));
        Debug.Log($"<color=magenta>[OY DAĞILIMI]</color> {voteDump}");

        var candidates = currentRoom.Players
            .Where(p => p.IsAlive && p.VoteCount > 0)
            .OrderByDescending(p => p.VoteCount)
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.Log("<color=magenta>[OY DAĞILIMI]</color> Hiç oy kullanılmamış (tüm VoteCount değerleri 0).");
            return null;
        }

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
}
