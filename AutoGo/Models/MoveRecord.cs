using System.Text.Json.Serialization;
using AutoGo.Common.Json;
using AutoGo.Enums;

namespace AutoGo.Models;

[JsonConverter(typeof(MoveConverter))]
public record MoveRecord(EStoneType StoneType, BoardCoords? Coords)
{
    public bool IsPass => Coords is null;
}
