using System;
using System.Collections.Generic;
using System.Linq;
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

    public List<GameStateNode> GetLongestPath(IReadOnlyDictionary<Guid, GameStateNode> gameStateNodes)
    {
        return GetLongestPath(this, gameStateNodes);
    }

    public static List<GameStateNode> GetLongestPath(GameStateNode root,
        IReadOnlyDictionary<Guid, GameStateNode> gameStateNodes)
    {
        var currentPath = new List<GameStateNode>();
        var bestPath = new List<GameStateNode>();

        Dfs(root);

        return bestPath;

        void Dfs(GameStateNode node)
        {
            currentPath.Add(node);
            if (currentPath.Count > bestPath.Count)
            {
                bestPath = new List<GameStateNode>(currentPath);
            }
            foreach (var childId in node.ChildrenIds)
            {
                Dfs(gameStateNodes[childId]);
            }
            currentPath.RemoveAt(currentPath.Count - 1);
        }
    }

    public IEnumerable<IReadOnlyList<GameStateNode>> GetAllPaths(IReadOnlyDictionary<Guid, GameStateNode> gameStateNodes)
    {
        return GetAllPaths(this, gameStateNodes);
    }

    public static IEnumerable<IReadOnlyList<GameStateNode>> GetAllPaths(GameStateNode root,
        IReadOnlyDictionary<Guid, GameStateNode> gameStateNodes)
    {
        var path = new List<GameStateNode>();
        var current = root;

        path.Add(current);

        // 1. compress the path
        while (current.ChildrenIds is { Count: 1 })
        {
            current = gameStateNodes[current.ChildrenIds[0]];
            path.Add(current);
        }

        // 2. arrive at a leaf node
        if (current.ChildrenIds.Count == 0)
        {
            yield return path;
            yield break;
        }

        // 3. branch out for each child
        foreach (var child in current.ChildrenIds.Select(id => gameStateNodes[id]))
        {
            foreach (var subPath in GetAllPaths(child, gameStateNodes))
            {
                var merged = new List<GameStateNode>(path.Count + subPath.Count);
                merged.AddRange(path);
                merged.AddRange(subPath);
                yield return merged;
            }
        }
    }
}
