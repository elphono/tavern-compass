namespace TavernCompass.NoDance.Core;

/// <summary>
/// One card of the player's row, as the mod reads it at the end of a frame. Three numbering systems coexist in the
/// client and this type keeps them apart: the position the layout sorts by, the processed position (the layout's
/// tie-break, late), and the server's real-time position.
/// </summary>
public sealed class BoardCard
{
    public BoardCard(int entityId, int visualPos, int processedPos, bool onServerBoard, int serverPos)
    {
        EntityId = entityId;
        VisualPos = visualPos;
        ProcessedPos = processedPos;
        OnServerBoard = onServerBoard;
        ServerPos = serverPos;
    }

    public int EntityId { get; }

    /// <summary>The card's own position (Card.GetZonePosition): what the row's layout sorts by.</summary>
    public int VisualPos { get; }

    /// <summary>The entity's processed ZONE_POSITION tag: the layout's tie-break, written when task lists are played.</summary>
    public int ProcessedPos { get; }

    /// <summary>The server still has the card in this row in real time (real-time zone PLAY, no controller change pending).</summary>
    public bool OnServerBoard { get; }

    /// <summary>The real-time ZONE_POSITION: the server's place, as soon as the packet is received.</summary>
    public int ServerPos { get; }

    /// <summary>The server's place is known and usable: a card off the server board, or without a place, keeps its own.</summary>
    public bool HasServerPlace => OnServerBoard && ServerPos > 0;

    public override string ToString() =>
        $"{EntityId}(shown {VisualPos}, processed {ProcessedPos}, server {(OnServerBoard ? ServerPos.ToString() : "gone")})";
}
