using System;
using UnityEngine;

[Serializable]
public class UserStats
{
    [Header("Basic Stats")]
    public int points;
    public int gamesPlayed;
    public int wins;
    public int losses;
    public int bingosCalled;

    public float WinRate
    {
        get
        {
            if (gamesPlayed <= 0)
            {
                return 0f;
            }

            return (float)wins / gamesPlayed;
        }
    }

    public int WinRatePercent
    {
        get
        {
            return Mathf.RoundToInt(WinRate * 100f);
        }
    }

    public void AddPoints(int amount)
    {
        points += amount;
    }

    public void RemovePoints(int amount)
    {
        points = Mathf.Max(0, points - amount);
    }

    public void AddGamePlayed()
    {
        gamesPlayed++;
    }

    public void AddWin()
    {
        wins++;
        gamesPlayed++;
    }

    public void AddLoss()
    {
        losses++;
        gamesPlayed++;
    }

    public void AddBingoCalled()
    {
        bingosCalled++;
    }

    public void ResetStats()
    {
        points = 0;
        gamesPlayed = 0;
        wins = 0;
        losses = 0;
        bingosCalled = 0;
    }
}