using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoGo.Enums;
using AutoGo.Infra;
using AutoGo.Models;
using AutoGo.Models.Dto;
using AutoGo.Service;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoGo.ViewModels;

public partial class GoPlayViewModel : ViewModelBase
{
    private readonly GameStateManager _gameStateManager = new();
    private readonly KataGoProcess _kataGoProcess = new();
    private readonly StringBuilder _logBuilder = new();
    private CancellationTokenSource? _kataGoCts;
    private KataGoService? _kataGoService;

    public GoPlayViewModel()
    {
        LastGameStateNode = _gameStateManager.CreateRootNode();
        GameStateNodes = _gameStateManager.GameStateNodes;
    }

    [ObservableProperty] public partial EStoneType[,] BoardData { get; set; } = new EStoneType[19, 19];

    [ObservableProperty] public partial EStoneType CurrentPlayer { get; set; } = EStoneType.Black;

    [ObservableProperty] public partial BoardCoords? LastMoveCoords { get; set; }

    [ObservableProperty] public partial string LogText { get; set; } = string.Empty;

    [ObservableProperty] public partial IEnumerable<AnalysisPoint> AnalysisPoints { get; set; } = [];

    [ObservableProperty] public partial GameStateNode LastGameStateNode { get; set; }

    [ObservableProperty] public partial Dictionary<Guid, GameStateNode> GameStateNodes { get; set; }

    [ObservableProperty] public partial bool IsAnalyzing { get; set; } = false;

    [ObservableProperty] public partial bool KataGoAutoReply { get; set; } = false;

    [RelayCommand]
    private async Task HandlePointClicked(BoardCoords coords)
    {
        if (KataGoPlayTheBestCommand.IsRunning)
        {
            return;
        }

        var moveRecord = new MoveRecord(CurrentPlayer, coords);
        TryPlay(moveRecord);
        if (KataGoAutoReply)
        {
            await KataGoPlayTheBestCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void PassTurn()
    {
        var moveRecord = new MoveRecord(CurrentPlayer, null);
        TryPlay(moveRecord);
    }

    [RelayCommand]
    private void ResetGame()
    {
        BoardData = new EStoneType[19, 19];
        CurrentPlayer = EStoneType.Black;
        LastMoveCoords = null;
        AnalysisPoints = [];
        _gameStateManager.Reset();
        LastGameStateNode = _gameStateManager.CreateRootNode();
        GameStateNodes = _gameStateManager.GameStateNodes;
    }

    [RelayCommand]
    private void UndoMove()
    {
        var parentNode = _gameStateManager.GetNodeOrDefault(LastGameStateNode.ParentId ?? Guid.Empty);
        if (parentNode is null)
        {
            return;
        }
        LastGameStateNode = parentNode;
        // _gameStateManager.RemoveNode(_lastGameStateNode.Id);
        BoardData = LastGameStateNode.BoardStateCache;
        CurrentPlayer = LastGameStateNode.Move?.StoneType == EStoneType.Black ? EStoneType.White : EStoneType.Black;
        LastMoveCoords = LastGameStateNode.Move?.Coords;
        AnalysisPoints = [];
        GameStateNodes = _gameStateManager.GameStateNodes;
    }

    [RelayCommand]
    private void HandleTreeNodeClicked(GameStateNode node)
    {
        if (!_gameStateManager.TryGetNode(node.Id, out var selectedNode)
            || selectedNode is null)
        {
            return;
        }

        LastGameStateNode = selectedNode;
        BoardData = selectedNode.BoardStateCache;
        CurrentPlayer = selectedNode.Move?.StoneType == EStoneType.Black ? EStoneType.White : EStoneType.Black;
        LastMoveCoords = selectedNode.Move?.Coords;
        AnalysisPoints = [];
    }

    [RelayCommand]
    private async Task KataGoAnalyzeAsync()
    {
        if (!await InitKataGo())
        {
            return;
        }
        // await _kataGoService!.SendAnalysis(_moveHistory);
        var nodePath = _gameStateManager.GetPathToRoot(LastGameStateNode.Id);
        var moveHistory = nodePath.Select(node => node.Move).Where(move => move is not null).Select(move => move!)
            .ToList();
        //if (moveHistory.Count == 0)
        //{
        //    AppendLog("No moves played yet. Cannot analyze.");
        //    return;
        //}
        var rootNode = nodePath.First();
        var initialMoveHistory = rootNode.InitialMoveHistory;
        IsAnalyzing = true;
        await _kataGoService!.SendAnalysis(moveHistory, initialMoveHistory);
    }

    [RelayCommand]
    private async Task KataGoPlayTheBestAsync()
    {
        if (!await InitKataGo())
        {
            return;
        }
        if (!IsAnalyzing && AnalysisPoints.Any())
        {
            PlayBestMove();
            return;
        }

        // else analysis and listen `IsAnalyzing` change
        var tcs = new TaskCompletionSource();
        PropertyChangedEventHandler handler = null!;
        handler = (sender, args) =>
        {
            if (args.PropertyName == nameof(IsAnalyzing) && !IsAnalyzing)
            {
                PropertyChanged -= handler;
                PlayBestMove();
                tcs.TrySetResult();
            }
        };
        PropertyChanged += handler;

        var token = KataGoPlayTheBestCommand.ExecutionTask?.AsyncState as CancellationTokenSource;
        try
        {
            await KataGoAnalyzeAsync();
            await tcs.Task;
        }
        finally
        {
            PropertyChanged -= handler;
        }
        return;

        void PlayBestMove()
        {
            if (!AnalysisPoints.Any())
            {
                return;
            }
            var bestCoords = AnalysisPoints.First().Coords;
            var bestMove = new MoveRecord(CurrentPlayer, bestCoords);
            TryPlay(bestMove);
        }
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
        _kataGoService.OnErrorResponseReceived += OnErrorResponseReceived;
        _kataGoCts = new CancellationTokenSource();
        _ = MonitorAnalysisAsync(_kataGoCts.Token);
        return true;
    }

    private void TryPlay(MoveRecord move)
    {
        var success = _gameStateManager.TryPlay(LastGameStateNode.Id, move, out var newGameStateNode);
        if (!success
            || newGameStateNode is null)
        {
            AppendLog($"Failed to play move at {move.Coords} for player {move.StoneType}.");
            return;
        }
        LastGameStateNode = newGameStateNode;
        BoardData = newGameStateNode.BoardStateCache;
        CurrentPlayer = CurrentPlayer == EStoneType.Black ? EStoneType.White : EStoneType.Black;
        LastMoveCoords = move.Coords;
        AnalysisPoints = [];
        GameStateNodes = _gameStateManager.GameStateNodes;
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
            Dispatcher.UIThread.Invoke(() =>
            {
                AnalysisPoints = analysisPoints;
                IsAnalyzing = result.IsDuringSearch;
            });
        }
    }

    private void OnErrorResponseReceived(KataGoErrorResponse errorResponse)
    {
        IsAnalyzing = false;
        AppendLog("Received error response from KataGo:");
        if (!string.IsNullOrEmpty(errorResponse.Error))
        {
            AppendLog($"Error: {errorResponse.Error}");
        }
        if (!string.IsNullOrEmpty(errorResponse.Warning))
        {
            AppendLog($"Warning: {errorResponse.Warning}");
        }
        if (!string.IsNullOrEmpty(errorResponse.Field))
        {
            AppendLog($"Field: {errorResponse.Field}");
        }
    }
}
