using System.Text.Json;
using AutoGo.Common.Json;
using AutoGo.Models.Dto;

namespace AutoGo.Service;

public static class KataGoProtocol
{
    public static KataGoResponse Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out _))
        {
            return JsonSerializer.Deserialize<KataGoErrorResponse>(json,
                KataGoJsonContext.Default.KataGoErrorResponse)!;
        }

        return JsonSerializer.Deserialize<KataGoAnalysisResult>(json, KataGoJsonContext.Default.KataGoAnalysisResult)!;
    }

    public static string Serialize(KataGoAnalysisQuery query)
    {
        return JsonSerializer.Serialize(query, KataGoJsonContext.Default.KataGoAnalysisQuery);
    }

    public static string Serialize(KataGoAnalysisActionQuery query)
    {
        return JsonSerializer.Serialize(query, KataGoJsonContext.Default.KataGoAnalysisActionQuery);
    }
}
