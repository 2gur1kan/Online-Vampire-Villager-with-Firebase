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

    // Bu üç alan, gece/oylama sonucu ve oyun bitişi gibi bilgileri TÜM
    // oyunculara (sadece host'un Debug.Log konsoluna değil) göstermek için
    // Firebase üzerinden yazılır. GameStatusText bunları dinleyip ekrana basar.
    public string LastNightMessage;

    public string LastVoteMessage;

    public string GameOverMessage;

    public Room()
    {
        ReadData = false;

        RoomID = Guid.NewGuid().ToString();

        HostID = "";

        TotalVote = 0;

        State = GameState.Lobby;

        Players = new List<Users>();

        SelectedDeadPlayer = null;

        LastNightMessage = "";

        LastVoteMessage = "";

        GameOverMessage = "";
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