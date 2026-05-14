using System.Collections.Generic;

namespace AutoGo.Models.Dto;

public class KataGoAnalysisQuery
{
    public string Id { get; set; } = string.Empty;
    public List<MoveRecord>? InitialStones { get; set; }
    public List<MoveRecord> Moves { get; set; } = [];
    public string? InitialPlayer { get; set; }
    public string Rules { get; set; } = "chinese-ogs";
    public double Komi { get; set; } = 7.5;
    public int BoardXSize { get; set; } = 19;
    public int BoardYSize { get; set; } = 19;
    public double ReportDuringSearchEvery { get; set; } = 1;
}

public class KataGoAnalysisActionQuery
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? TerminateId { get; set; }
    public List<int>? TurnNumbers { get; set; }
}

public abstract class KataGoResponse
{
    public string Id { get; set; } = string.Empty;
}

public class KataGoErrorResponse : KataGoResponse
{
    public string Error { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
    public string? Field { get; set; }
}

public class KataGoAnalysisResult : KataGoResponse
{
    public bool IsDuringSearch { get; set; }
    public int TurnNumber { get; set; }
    public List<KataGoAnalysisMoveInfo> MoveInfos { get; set; } = [];
    public KataGoAnalysisRootInfo RootInfo { get; set; } = new();
}

public class KataGoAnalysisMoveInfo
{
    public double Lcb { get; set; }
    public string Move { get; set; } = string.Empty;
    public int Order { get; set; }
    public double Prior { get; set; }
    public List<string> Pv { get; set; } = [];
    public double ScoreLead { get; set; }
    public double ScoreMean { get; set; }
    public double ScoreSelfplay { get; set; }
    public double ScoreStdev { get; set; }
    public double Utility { get; set; }
    public double UtilityLcb { get; set; }
    public int Visits { get; set; }
    public int EdgeVisits { get; set; }
    public double Winrate { get; set; }
}

public class KataGoAnalysisRootInfo
{
    public string CurrentPlayer { get; set; } = string.Empty;
    public double Lcb { get; set; }
    public double ScoreLead { get; set; }
    public double ScoreSelfplay { get; set; }
    public double ScoreStdev { get; set; }
    public string SymHash { get; set; } = string.Empty;
    public string ThisHash { get; set; } = string.Empty;
    public double Utility { get; set; }
    public int Visits { get; set; }
    public double Winrate { get; set; }
}
