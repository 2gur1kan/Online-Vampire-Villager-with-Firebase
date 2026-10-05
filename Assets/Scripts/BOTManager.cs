using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Odaya sistem botlarını ekleme ve bot oylaması ile ilgili TÜM mantık
// buradadır. GameManager sadece faz akışını yönetir, botların nasıl
// davrandığı tamamen bu script'in sorumluluğundadır (işler ayrışmış olur).
//
// ÖNEMLİ DÜZELTME: Eskiden BotVoteFirebase/BotVoteAllFirebase, rolü/hayatta
// olma durumu uyan HERKESİ (gerçek oyuncular dahil) "oy veren" sayıyordu.
// Bu yüzden gerçek oyuncunun rolü Vampir/Doktor olduğunda ya da köy
// oylamasında, sistem onun ADINA da rastgele bir oy atıyordu — yani
// oyuncunun kendi seçimiyle çakışan/onu ezen bir "bot oyu" oluyordu. Artık
// her iki metot da SADECE p.IsBot == true olan oyuncuları oy veren olarak
// alıyor; gerçek oyuncunun oyu SADECE PlayerTurnController.SubmitVote
// üzerinden (kendi tıklamasıyla) gelir.
public class BOTManager : MonoBehaviour
{
    public static BOTManager Instance;

    [Tooltip("Kapatılırsa sistem botları odaya hiç eklenmez.")]
    public bool UnlockBots = true;

    private static readonly List<string> SystemBotNames = new List<string>
    {
        "Gürkan", "Abuzer", "Fettah",
    };

    private FireBaseDataBase db;

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
    }

    // =====================================================
    // BOT EKLEME
    // =====================================================

    public async Task SpawnSystemBotsAsync()
    {
        if (!UnlockBots) return;

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
    // BOT OYLAMA (SADECE p.IsBot == true OLAN OYUNCULAR OY VERİR)
    // =====================================================

    public void BotVoteFirebase(RoleType role, Room currentRoom)
    {
        var voters = currentRoom.Players.Where(p => p.Role == role && p.IsAlive && p.IsBot).ToList();

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

    public void BotVoteAllFirebase(Room currentRoom)
    {
        var aliveBots = currentRoom.Players.Where(p => p.IsAlive && p.IsBot).ToList();

        if (aliveBots.Count == 0) return;

        // Hedef havuzu tüm hayatta olanlardır (botlar birbirine de oy verebilir);
        // sadece OY VERENLER bot olmak zorunda.
        var allAlivePlayers = currentRoom.Players.Where(p => p.IsAlive).ToList();

        foreach (var bot in aliveBots)
        {
            Users target = allAlivePlayers[UnityEngine.Random.Range(0, allAlivePlayers.Count)];
            db.VotePlayer(target.UserID);
            Debug.Log($"[BOT KÖY OYU] {bot.UserName} -> {target.UserName} kişisine oy verdi.");
        }
    }
}
