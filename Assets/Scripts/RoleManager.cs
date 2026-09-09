using System;
using Photon.Pun;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public static class RoleManager
{
    public const string KillerActorNumberKey = "KillerActorNumber";
    public const string RouletteStartTimeKey = "RouletteStartTime";
    public const string RouletteSeedKey = "RouletteSeed";

    public static bool HasKiller
    {
        get
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return false;

            return GetKillerActorNumber() > 0;
        }
    }

    public static int GetKillerActorNumber()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return -1;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
            KillerActorNumberKey,
            out object value))
        {
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return -1;
            }
        }

        return -1;
    }

    public static double GetRouletteStartTime()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return 0.0;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
            RouletteStartTimeKey,
            out object value))
        {
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0.0;
            }
        }

        return 0.0;
    }

    public static int GetRouletteSeed()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return 0;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
            RouletteSeedKey,
            out object value))
        {
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    public static void SetRole(
        int killerActorNumber,
        double rouletteStartTime,
        int rouletteSeed)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonHashtable properties = new PhotonHashtable
        {
            { KillerActorNumberKey, killerActorNumber },
            { RouletteStartTimeKey, rouletteStartTime },
            { RouletteSeedKey, rouletteSeed }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    public static void ClearRole()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonHashtable properties = new PhotonHashtable
        {
            { KillerActorNumberKey, -1 },
            { RouletteStartTimeKey, 0.0 },
            { RouletteSeedKey, 0 }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }
}