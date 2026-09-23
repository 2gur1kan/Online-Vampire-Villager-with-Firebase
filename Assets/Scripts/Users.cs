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

    public int VoteCount;

    public Users()
    {
        UserID = Guid.NewGuid().ToString();

        UserName = "";

        Role = RoleType.Villager;

        IsAlive = true;

        IsHost = false;

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

    public Room()
    {
        ReadData = false;

        RoomID = Guid.NewGuid().ToString();

        HostID = "";

        TotalVote = 0;

        State = GameState.Lobby;

        Players = new List<Users>();

        SelectedDeadPlayer = null;
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