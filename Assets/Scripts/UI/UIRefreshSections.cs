using System;

namespace BingoGame.UI
{
    [Flags]
    public enum LobbyUIRefresh
    {
        None = 0,
        Header = 1,
        Players = 2,
        Board = 4,
        BoardControls = 8,
        GameInfo = 16,
        CustomInfo = 32,
        PlayerState = Header | Players,
        Settings = PlayerState | GameInfo,
        All = Header | Players | Board | BoardControls | GameInfo | CustomInfo
    }

    [Flags]
    public enum GameUIRefresh
    {
        None = 0,
        Header = 1,
        Players = 2,
        Balls = 4,
        Board = 8,
        BoardControls = 16,
        GameInfo = 32,
        CustomInfo = 64,
        PlayerState = Header | Players | BoardControls,
        Gameplay = PlayerState | Balls,
        All = Gameplay | Board | GameInfo | CustomInfo
    }
}
