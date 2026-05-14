using System.Collections.Generic;
using System.Text.Json.Serialization;
using AutoGo.Models;
using AutoGo.Models.Dto;

namespace AutoGo.Common.Json;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(KataGoAnalysisQuery))]
[JsonSerializable(typeof(KataGoAnalysisActionQuery))]
[JsonSerializable(typeof(MoveRecord))]
[JsonSerializable(typeof(List<MoveRecord>))]
[JsonSerializable(typeof(KataGoErrorResponse))]
[JsonSerializable(typeof(KataGoAnalysisResult))]
[JsonSerializable(typeof(KataGoAnalysisMoveInfo))]
[JsonSerializable(typeof(KataGoAnalysisRootInfo))]
internal partial class KataGoJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(MoveRecord))]
[JsonSerializable(typeof(List<MoveRecord>))]
internal partial class AppJsonContext : JsonSerializerContext;
