using System.Collections.Generic;

public class LobbyWorkBatchResult
{
    #region Fields

    public readonly List<LobbyPlayerViewData> addedPlayers = new List<LobbyPlayerViewData>();
    public readonly List<LobbyPlayerBoardViewData> changedBoards = new List<LobbyPlayerBoardViewData>();

    public bool HasChanges => addedPlayers.Count > 0 || changedBoards.Count > 0;

    #endregion
}
