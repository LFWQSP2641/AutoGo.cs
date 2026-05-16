using System;
using System.Collections.Generic;
using System.Linq;
using AutoGo.Enums;
using AutoGo.Models;

namespace AutoGo.Service;

public class GameStateManager(int boardSize = 19)
{
    private readonly Dictionary<Guid, GameStateNode> _gameStateNodes = new();

    public GameStateNode CreateRootNode(IReadOnlyList<MoveRecord> initialMoveHistory)
    {
        var board = new EStoneType[boardSize, boardSize];
        foreach (var move in initialMoveHistory)
        {
            if (move.IsPass || move.Coords == null)
            {
                continue;
            }
            var (x, y) = move.Coords.Value;
            board[x, y] = move.StoneType;
            board = CleanNoLibertiesStones(board);
        }
        var rootNode = new GameStateNode
        {
            InitialMoveHistory = initialMoveHistory,
            BoardStateCache = board,
        };
        _gameStateNodes[rootNode.Id] = rootNode;
        return rootNode;
    }

    public GameStateNode CreateRootNode(EStoneType[,] initialBoardState)
    {
        var moveList = new List<MoveRecord>();
        for (var x = 0; x < boardSize; x++)
        {
            for (var y = 0; y < boardSize; y++)
            {
                if (initialBoardState[x, y] == EStoneType.None)
                {
                    continue;
                }
                moveList.Add(new MoveRecord(initialBoardState[x, y], new BoardCoords { X = x, Y = y }));
            }
        }
        return CreateRootNode(moveList);
    }

    public GameStateNode CreateRootNode()
    {
        return CreateRootNode([]);
    }

    public void Reset()
    {
        _gameStateNodes.Clear();
    }

    public List<GameStateNode> GetAllRootNodes()
    {
        return _gameStateNodes.Values.Where(node => node.ParentId == null).ToList();
    }

    public void RemoveNode(Guid nodeId)
    {
        if (!_gameStateNodes.ContainsKey(nodeId))
        {
            throw new ArgumentException("Node not found", nameof(nodeId));
        }
        var childNodes = _gameStateNodes.Values.Where(node => node.ParentId == nodeId).ToList();
        foreach (var child in childNodes)
        {
            RemoveNode(child.Id);
        }
        _gameStateNodes.Remove(nodeId);
        var parentNode = _gameStateNodes.GetValueOrDefault(_gameStateNodes[nodeId].ParentId ?? Guid.Empty);
        if (parentNode != null)
        {
            _gameStateNodes[parentNode.Id] = parentNode with
            {
                ChildrenIds = parentNode.ChildrenIds.Where(id => id != nodeId).ToList(),
            };
        }
    }

    public List<GameStateNode> GetPathToRoot(Guid nodeId)
    {
        if (!_gameStateNodes.TryGetValue(nodeId, out var currentNode))
        {
            throw new ArgumentException("Node not found", nameof(nodeId));
        }

        var path = new List<GameStateNode>();

        while (currentNode != null)
        {
            path.Add(currentNode);
            currentNode = currentNode.ParentId.HasValue &&
                          _gameStateNodes.TryGetValue(currentNode.ParentId.Value, out var node)
                ? node
                : null;
        }

        path.Reverse();
        return path;
    }

    public bool TryPlay(Guid parentId, MoveRecord move, out GameStateNode? newNode)
    {
        newNode = null!;
        if (!_gameStateNodes.TryGetValue(parentId, out var parentNode))
        {
            return false;
        }
        var newBoardState = CheckAndPlay(parentNode.BoardStateCache, move,
            parentNode.Move);
        if (newBoardState == null)
        {
            return false; // Invalid move
        }
        newNode = new GameStateNode
        {
            ParentId = parentNode.Id,
            Move = move,
            BoardStateCache = newBoardState,
        };
        _gameStateNodes[newNode.Id] = newNode;
        _gameStateNodes[parentNode.Id] = parentNode with
        {
            ChildrenIds = parentNode.ChildrenIds.Append(newNode.Id).ToList(),
        };
        return true;
    }

    public bool TryGetNode(Guid nodeId, out GameStateNode? node)
    {
        return _gameStateNodes.TryGetValue(nodeId, out node);
    }

    public GameStateNode? GetNodeOrDefault(Guid nodeId, GameStateNode? defaultNode = null)
    {
        return _gameStateNodes!.GetValueOrDefault(nodeId, defaultNode);
    }

    private bool CheckOutbounds(BoardCoords coords)
    {
        var (x, y) = coords;
        return x < 0 || x >= boardSize || y < 0 || y >= boardSize;
    }

    private bool HasLiberties(EStoneType[,] board, BoardCoords coords, EStoneType stoneType, bool[,] visited)
    {
        var (x, y) = coords;
        if (CheckOutbounds(coords) || visited[x, y] || board[x, y] != stoneType)
        {
            return false;
        }

        visited[x, y] = true;
        if ((x > 0 && board[x - 1, y] == EStoneType.None)
            || (x < boardSize - 1 && board[x + 1, y] == EStoneType.None)
            || (y > 0 && board[x, y - 1] == EStoneType.None)
            || (y < boardSize - 1 && board[x, y + 1] == EStoneType.None))
        {
            return true;
        }

        return HasLiberties(board, new(x - 1, y), stoneType, visited)
               || HasLiberties(board, new(x + 1, y), stoneType, visited)
               || HasLiberties(board, new(x, y - 1), stoneType, visited)
               || HasLiberties(board, new(x, y + 1), stoneType, visited);
    }

    private HashSet<BoardCoords> GetSurroundingStones(EStoneType[,] board, BoardCoords coords,
        EStoneType stoneType, bool[,] visited)
    {
        // Check out of bounds
        if (CheckOutbounds(coords))
        {
            return [];
        }

        if (visited[coords.X, coords.Y])
        {
            return [];
        }

        visited[coords.X, coords.Y] = true;
        if (board[coords.X, coords.Y] != stoneType)
        {
            return [];
        }

        var surroundingStones = new HashSet<BoardCoords> { coords };
        var directions = new (int dx, int dy)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
        foreach (var (dx, dy) in directions)
        {
            var newX = coords.X + dx;
            var newY = coords.Y + dy;
            var newCoords = new BoardCoords { X = newX, Y = newY };
            if (CheckOutbounds(newCoords))
            {
                continue;
            }
            var newSurroundingStones = GetSurroundingStones(board, newCoords, stoneType, visited);
            surroundingStones.UnionWith(newSurroundingStones);
        }

        return surroundingStones;
    }

    private EStoneType[,] CleanNoLibertiesStones(EStoneType[,] board)
    {
        var visited = new bool[boardSize, boardSize];
        var cleanedBoard = (EStoneType[,])board.Clone();
        for (var x = 0; x < boardSize; x++)
        {
            for (var y = 0; y < boardSize; y++)
            {
                if (cleanedBoard[x, y] == EStoneType.None || visited[x, y])
                {
                    continue;
                }
                var stoneType = cleanedBoard[x, y];
                if (HasLiberties(cleanedBoard, new BoardCoords { X = x, Y = y }, stoneType, visited))
                {
                    continue;
                }
                var surroundingStones = GetSurroundingStones(cleanedBoard,
                    new BoardCoords { X = x, Y = y }, stoneType, new bool[boardSize, boardSize]);
                foreach (var coords in surroundingStones)
                {
                    cleanedBoard[coords.X, coords.Y] = EStoneType.None;
                }
            }
        }
        return cleanedBoard;
    }

    private EStoneType[,]? CheckAndPlay(EStoneType[,] board, MoveRecord nextMove, MoveRecord? lastMove = null)
    {
        if (nextMove.IsPass)
        {
            return board;
        }

        var coords = nextMove.Coords!.Value;
        if (CheckOutbounds(coords))
        {
            return null;
        }

        if (board[coords.X, coords.Y] != EStoneType.None)
        {
            return null; // Invalid move
        }

        var boardCopy = (EStoneType[,])board.Clone();
        var aroundPointList = new List<BoardCoords>
        {
            coords with { X = coords.X - 1 },
            coords with { X = coords.X + 1 },
            coords with { Y = coords.Y - 1 },
            coords with { Y = coords.Y + 1 },
        };
        // Remove the points that are out of bounds
        aroundPointList = aroundPointList
            .Where(CheckOutbounds).ToList();
        // 1. Check the enemy stones around the current move.
        // 2. If they have no liberties, check whether they can be captured.
        // 3. If the situation is a ko, return null.
        // 4. If it is not a ko, capture them.
        // 5. Check whether the current move has liberties; if not, return null.
        // 6. If the current move has liberties, return the new board.
        var enemyType = nextMove.StoneType == EStoneType.Black ? EStoneType.White : EStoneType.Black;
        var boardAfterMove = (EStoneType[,])boardCopy.Clone();
        boardAfterMove[coords.X, coords.Y] = nextMove.StoneType;
        var enemyCapturedMap = new Dictionary<BoardCoords, bool>();
        foreach (var aroundPoint in aroundPointList)
        {
            var visited = new bool[19, 19];
            if (board[aroundPoint.X, aroundPoint.Y] != enemyType)
            {
                continue;
            }

            var hasLiberties = HasLiberties(boardAfterMove, aroundPoint, enemyType, visited);
            enemyCapturedMap[aroundPoint] = hasLiberties;
        }

        var hasEnemyCaptured = enemyCapturedMap.Values.Any(hasLiberties => !hasLiberties);
        if (hasEnemyCaptured)
        {
            // Simple check for ko
            // TODO: superko check
            if (!enemyCapturedMap.GetValueOrDefault(lastMove?.Coords ?? default, true))
            {
                var surroundingStones =
                    GetSurroundingStones(board, lastMove!.Coords!.Value, enemyType, new bool[19, 19]);
                var hasLibertiesForNextMove =
                    HasLiberties(boardAfterMove, coords, nextMove.StoneType, new bool[19, 19]);
                if (surroundingStones.Count == 1
                    && !hasLibertiesForNextMove)
                {
                    return null; // Ko situation
                }
            }
        }

        // Capture enemy stones
        foreach (var stone in from kvp in enemyCapturedMap
                 where !kvp.Value
                 let visited = new bool[19, 19]
                 select GetSurroundingStones(boardAfterMove, kvp.Key, enemyType, visited)
                 into surroundingStones
                 from stone in surroundingStones
                 select stone)
        {
            boardAfterMove[stone.X, stone.Y] = EStoneType.None;
        }

        if (hasEnemyCaptured)
        {
            return boardAfterMove;
        }

        // Check whether the current move has liberties; if not, return null.
        var visitedForCurrentMove = new bool[19, 19];
        var hasLibertiesForCurrentMove =
            HasLiberties(boardAfterMove, coords, nextMove.StoneType, visitedForCurrentMove);
        return hasLibertiesForCurrentMove ? boardAfterMove : null;
    }
}
