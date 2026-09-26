using System;
using System.Collections.Generic;

[Serializable]
public class Users
{
    public string UserID;

    public string UserName;

    public RoleType Role;

    public bool IsAlive;

    public bool IsHost;

    public bool IsBot;

    public bool HasVotedThisPhase;

    public int VoteCount;

    public Users()
    {
        UserID = Guid.NewGuid().ToString();

        UserName = "";

        Role = RoleType.Villager;

        IsAlive = true;

        IsHost = false;

        IsBot = false;

        HasVotedThisPhase = false;

        VoteCount = 0;
    }
}

[Serializable]
public class Room
{
    public bool ReadData;

    public string RoomID;

    public string HostID;

    public int TotalVote;

    public GameState State;

    public List<Users> Players;

    public string SelectedDeadPlayer;

    public string OldDoctorVote;

    // Gece/oylama sonucu ve oyun bitişi gibi bilgileri TÜM oyunculara (sadece
    // host'un Debug.Log konsoluna değil) göstermek için Firebase üzerinden
    // yazılır. TEK bir alan olması bilinçli: her yeni olay bunun ÜZERİNE
    // yazılır, böylece GameStatusText "hangi alan değişti" diye ayrı ayrı
    // takip etmek zorunda kalmaz ve iki farklı olayın metni aynı olsa bile
    // (örn. iki gece üst üste "kimse saldırıya uğramadı") ekran asla eski
    // bir olayda takılı kalmaz.
    public string LastEventMessage;

    // Sunucu tarafından yazılan, TÜM cihazların aynı geri sayımı göstermesini
    // sağlayan alanlar. PhaseStartTimeMillis, ServerValue.Timestamp ile
    // (cihazın kendi saati değil, Firebase sunucusunun saatiyle) yazılır.
    public double PhaseStartTimeMillis;

    public float PhaseDurationSeconds;

    public Room()
    {
        ReadData = false;

        RoomID = Guid.NewGuid().ToString();

        HostID = "";

        TotalVote = 0;

        State = GameState.Lobby;

        Players = new List<Users>();

        SelectedDeadPlayer = null;

        LastEventMessage = "";

        PhaseStartTimeMillis = 0;

        PhaseDurationSeconds = 0f;
    }
}

public enum RoleType
{
    Villager,
    Doctor,
    Vampire
}

public enum GameState
{
    Lobby,

    RoleReveal,

    Night,

    VampireVote,

    DoctorVote,

    Day,

    Voting,

    Result,

    EndGame
}