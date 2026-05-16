using System;
using System.Collections.Generic;
using AutoGo.Enums;

namespace AutoGo.Models;

public record GameStateNode(
    Guid Id,
    Guid? ParentId,
    IReadOnlyList<Guid> ChildrenIds,
    MoveRecord? Move, // Null if root node
    IReadOnlyList<MoveRecord>? InitialMoveHistory, // For root node, this is the initial move history. For non-root nodes, this is null.
    EStoneType[,] BoardStateCache // For GUI and quick access, generally considered reliable
)
{
    public GameStateNode() :
        this(Guid.CreateVersion7(), null, new List<Guid>(), null, new List<MoveRecord>(), new EStoneType[19, 19])
    {
    }
}
