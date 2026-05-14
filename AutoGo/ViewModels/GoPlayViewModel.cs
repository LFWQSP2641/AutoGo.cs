using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoGo.Enums;
using AutoGo.Infra;
using AutoGo.Models;
using AutoGo.Service;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoGo.ViewModels;

public partial class GoPlayViewModel : ViewModelBase
{
    private readonly KataGoProcess _kataGoProcess = new();
    private readonly StringBuilder _logBuilder = new();
    private readonly List<MoveRecord> _moveHistory = [];
    private CancellationTokenSource? _kataGoCts;
    private KataGoService? _kataGoService;

    [ObservableProperty] public partial EStoneType[,] BoardData { get; set; } = new EStoneType[19, 19];

    [ObservableProperty] public partial EStoneType CurrentPlayer { get; set; } = EStoneType.Black;

    [ObservableProperty] public partial string LogText { get; set; } = string.Empty;

    [ObservableProperty] public partial List<AnalysisPoint> AnalysisPoints { get; set; } = [];

    [RelayCommand]
    private void HandlePointClicked(BoardCoords coords)
    {
        if (BoardData[coords.X, coords.Y] != EStoneType.None)
        {
            return;
        }

        var moveRecord = new MoveRecord(CurrentPlayer, coords);
        var newBoard = CheckAndPlay(BoardData, moveRecord, _moveHistory.LastOrDefault());
        if (newBoard == null)
        {
            return;
        }

        BoardData = newBoard;
        CurrentPlayer = CurrentPlayer == EStoneType.Black ? EStoneType.White : EStoneType.Black;
        AnalysisPoints = [];
        _moveHistory.Add(moveRecord);
    }

    [RelayCommand]
    private async Task KataGoAnalyzeAsync()
    {
        if (!await InitKataGo())
        {
            return;
        }
        await _kataGoService!.SendAnalysis(_moveHistory);
    }

    private async Task<bool> InitKataGo()
    {
        if (_kataGoService is not null)
        {
            return true;
        }
        _kataGoProcess.OutputReceived += AppendLog;
        _kataGoProcess.ErrorReceived += AppendLog;
        const string exePath = "D:/Software/GoAI/katago-v1.16.4-eigenavx2-windows-x64/katago.exe";
        const string configPath = "D:/Software/GoAI/katago-v1.16.4-eigenavx2-windows-x64/analysis_test.cfg";
        const string modelPath =
            "D:/Software/GoAI/katago-v1.16.4-eigenavx2-windows-x64/kata1-b6c96-s175395328-d26788732.txt.gz";
        var result = await _kataGoProcess.StartAndWaitInit(exePath, configPath, modelPath);
        _kataGoProcess.OutputReceived -= AppendLog;
        _kataGoProcess.ErrorReceived -= AppendLog;
        if (!result)
        {
            AppendLog("Failed to start KataGo process.");
            return false;
        }
        _kataGoService = new KataGoService(_kataGoProcess);
        _kataGoCts = new CancellationTokenSource();
        _ = MonitorAnalysisAsync(_kataGoCts.Token);
        return true;
    }

    private static EStoneType[,]? CheckAndPlay(EStoneType[,] board, MoveRecord nextMove, MoveRecord? lastMove = null)
    {
        if (nextMove.IsPass)
        {
            return board;
        }

        var coords = nextMove.Coords!.Value;
        if (coords.X < 0 || coords.X >= 19 || coords.Y < 0 || coords.Y >= 19)
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
            .Where(aroundPoint => aroundPoint.X is >= 0 and < 19 && aroundPoint.Y is >= 0 and < 19).ToList();
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

    private static bool HasLiberties(EStoneType[,] board, BoardCoords coords, EStoneType stoneType, bool[,] visited)
    {
        var (x, y) = coords;
        if (x < 0 || x >= 19 || y < 0 || y >= 19 || visited[x, y] || board[x, y] != stoneType)
        {
            return false;
        }

        visited[x, y] = true;
        if ((x > 0 && board[x - 1, y] == EStoneType.None)
            || (x < 18 && board[x + 1, y] == EStoneType.None)
            || (y > 0 && board[x, y - 1] == EStoneType.None)
            || (y < 18 && board[x, y + 1] == EStoneType.None))
        {
            return true;
        }

        return HasLiberties(board, new(x - 1, y), stoneType, visited)
               || HasLiberties(board, new(x + 1, y), stoneType, visited)
               || HasLiberties(board, new(x, y - 1), stoneType, visited)
               || HasLiberties(board, new(x, y + 1), stoneType, visited);
    }

    private static HashSet<BoardCoords> GetSurroundingStones(EStoneType[,] board, BoardCoords coords,
        EStoneType stoneType, bool[,] visited)
    {
        // Check out of bounds
        if (coords.X < 0 || coords.X >= board.GetLength(0) || coords.Y < 0 || coords.Y >= board.GetLength(1))
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
            if (newX < 0 || newX >= board.GetLength(0) || newY < 0 || newY >= board.GetLength(1))
            {
                continue;
            }

            var newCoords = new BoardCoords { X = newX, Y = newY };
            var newSurroundingStones = GetSurroundingStones(board, newCoords, stoneType, visited);
            surroundingStones.UnionWith(newSurroundingStones);
        }

        return surroundingStones;
    }

    private void AppendLog(string message)
    {
        _logBuilder.AppendLine(message);
        Dispatcher.UIThread.Invoke(() => { LogText = _logBuilder.ToString(); });
    }

    private async Task MonitorAnalysisAsync(CancellationToken ct)
    {
        await foreach (var result in _kataGoService!.AnalysisReader.ReadAllAsync(ct))
        {
            var analysisPoints = AnalysisPoint.FromKataGoMoveInfos(result.MoveInfos);
            AppendLog($"Received analysis result for query {result.Id} with {analysisPoints.Count} move infos.");
            Dispatcher.UIThread.Invoke(() => { AnalysisPoints = analysisPoints; });
        }
    }
}
