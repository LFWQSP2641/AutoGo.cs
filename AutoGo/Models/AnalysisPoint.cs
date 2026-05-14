using System;
using System.Collections.Generic;
using System.Linq;
using AutoGo.Models.Dto;
using Avalonia.Media;

namespace AutoGo.Models;

public class AnalysisPoint
{
    public BoardCoords Coords { get; set; }
    public double WinRate { get; set; }
    public string ScoreLabel { get; set; } = string.Empty;
    public int Order { get; set; }
    public double NormalizedScore { get; set; }
    public Color ThemeColor { get; set; }
    public double Opacity { get; set; }
    public List<BoardCoords> VariationPath { get; set; } = [];

    public static AnalysisPoint FromKataGoMoveInfo(KataGoAnalysisMoveInfo moveInfo)
    {
        var winRate = 0.5 + moveInfo.ScoreLead / 100.0;
        var scoreLabel = moveInfo.ScoreLead > 0
            ? $"+{moveInfo.ScoreLead:F1}"
            : $"{moveInfo.ScoreLead:F1}";
        var normalized = Math.Tanh(moveInfo.ScoreLead / 10.0);
        var t = Math.Min(1.0, Math.Max(0.0, (normalized + 1.0) / 2.0));
        var r = (byte)(Colors.Red.R * (1 - t) + Colors.Green.R * t);
        var g = (byte)(Colors.Red.G * (1 - t) + Colors.Green.G * t);
        var b = (byte)(Colors.Red.B * (1 - t) + Colors.Green.B * t);
        var linearColor = Color.FromRgb(r, g, b);

        return new AnalysisPoint
        {
            Coords = (BoardCoords)BoardCoords.ParseGtpVertex(moveInfo.Move)!,
            WinRate = winRate,
            ScoreLabel = scoreLabel,
            Order = moveInfo.Order,
            NormalizedScore = normalized,
            ThemeColor = linearColor,
            Opacity = 0.4 + 0.6 * Math.Abs(normalized),
            VariationPath = moveInfo.Pv.Select(ps => BoardCoords.ParseGtpVertex(ps)).Where(c => c != null)
                .Cast<BoardCoords>()
                .ToList(),
        };
    }

    public static List<AnalysisPoint> FromKataGoMoveInfos(List<KataGoAnalysisMoveInfo> moveInfos)
    {
        var maxVisit = moveInfos.Max(m => m.Visits);
        var points = moveInfos.Select(FromKataGoMoveInfo)
            .Select((p, i) =>
            {
                var visitFactor = moveInfos[i].Visits / (double)maxVisit;
                p.Opacity = 0.2 + 0.6 * visitFactor; // Adjust opacity based on visit count
                return p;
            }).ToList();
        return points;
    }
}
