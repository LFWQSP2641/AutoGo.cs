using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using AutoGo.Enums;
using AutoGo.Infra;
using AutoGo.Models;
using AutoGo.Models.Dto;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoGo.Service;

public class KataGoService : ObservableObject
{
    private readonly Channel<KataGoAnalysisResult> _channel = Channel.CreateBounded<KataGoAnalysisResult>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true,
        });

    private readonly KataGoProcess _driver;
    private string? _currentQueryId;

    public KataGoService(KataGoProcess driver)
    {
        _driver = driver;

        _driver.OutputReceived += OnRawOutputReceived;
        _driver.ErrorReceived += msg => Trace.WriteLine($"KataGo Log: {msg}");
    }

    public ChannelReader<KataGoAnalysisResult> AnalysisReader => _channel.Reader;

    public async Task SendAnalysis(
        IEnumerable<MoveRecord> moves,
        IEnumerable<MoveRecord>? initialStones = null,
        EStoneType? initialPlayer = null)
    {
        _currentQueryId = Guid.NewGuid().ToString();

        var query = new KataGoAnalysisQuery
        {
            Id = _currentQueryId,
            Moves = moves.ToList(),
            InitialStones = initialStones?.ToList(),
            InitialPlayer = initialPlayer switch
            {
                EStoneType.Black => "B",
                EStoneType.White => "W",
                _ => null,
            },
            ReportDuringSearchEvery = 0.05,
        };

        var jsonQuery = KataGoProtocol.Serialize(query);
        await _driver.SendLine(jsonQuery);
    }

    public async Task SendClearCache()
    {
        _currentQueryId = Guid.NewGuid().ToString();

        var query = new KataGoAnalysisActionQuery { Id = _currentQueryId, Action = "clear_cache" };

        var jsonQuery = KataGoProtocol.Serialize(query);
        await _driver.SendLine(jsonQuery);
    }

    public async Task SendStopAnalyze()
    {
        _currentQueryId = Guid.NewGuid().ToString();

        var query = new KataGoAnalysisActionQuery { Id = _currentQueryId, Action = "terminate_all" };

        var jsonQuery = KataGoProtocol.Serialize(query);
        await _driver.SendLine(jsonQuery);
    }

    private void OnRawOutputReceived(string rawJson)
    {
        var response = KataGoProtocol.Parse(rawJson);

        if (response is KataGoErrorResponse error)
        {
            Trace.WriteLine($"KataGo Error: {error.Error}");
        }

        if (!string.IsNullOrEmpty(response.Id) && response.Id != _currentQueryId)
        {
            return;
        }

        if (response is KataGoAnalysisResult result)
        {
            _channel.Writer.TryWrite(result);
        }
    }
}
