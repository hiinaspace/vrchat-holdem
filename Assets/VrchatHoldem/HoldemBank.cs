using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
/// <summary>
/// tracks per-player chips in a way that's robust to disconnects,
/// with a way for the room master to adjust chips.
/// 
/// mostly mirrors the chips in HoldemGame, but in the case a player disconnects
/// then rejoins and the master still retains this, they can come back with their chip
/// count intact and without the master having to adjust up (or down) for them.
///
/// In theory this can also be shared between multiple tables, udon networking willing.
/// </summary>
public class HoldemBank : UdonSharpBehaviour
{
    // linear-probing arrays are good enough for infrequent access.
    // 1024 should be enough for anyone
    string[] playerNames = new string[1024];
    int[] chipTotal = new int[1024];
    int size = 0;

    void Start()
    {
    }

    public int GetBalance(VRCPlayerApi player)
    {
        var name = player == null ? "(Editor)" : player.displayName;
        for (int i = 0; i < size; ++i)
        {
            if (playerNames[i] == name)
            {
                return chipTotal[i];
            }
        }
        return -1;
    }

    public void SetBalance(VRCPlayerApi player, int balance)
    {
        var name = player == null ? "(Editor)" : player.displayName;
        for (int i = 0; i < size; ++i)
        {
            if (playerNames[i] == name)
            {
                chipTotal[i] = balance;
                return;
            }
        }
        playerNames[size] = name;
        chipTotal[size] = balance;
        size++;
    }
}