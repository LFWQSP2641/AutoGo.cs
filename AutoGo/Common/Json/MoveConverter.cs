using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoGo.Enums;
using AutoGo.Models;

namespace AutoGo.Common.Json;

public class MoveConverter : JsonConverter<MoveRecord>
{
    public override MoveRecord? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected array");
        }
        reader.Read();
        var color = reader.GetString();
        if (color is null)
        {
            throw new JsonException("Invalid color");
        }
        reader.Read();
        var pos = reader.GetString();
        if (pos is null)
        {
            throw new JsonException("Invalid position");
        }
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Array had more than 2 elements");
        }

        // ["B", "Q4"] to EStoneType.Black and BoardCoords(16, 4)
        var stoneType = EStoneTypeConverter.FromGtpColor(color);
        var coords = BoardCoords.ParseGtpVertex(pos);
        return new MoveRecord(stoneType, coords);
    }

    public override void Write(Utf8JsonWriter writer, MoveRecord value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var color = value.StoneType.ToGtpColor();
        writer.WriteStringValue(color);
        var pos = value.Coords?.ToGtpVertex() ?? "pass";
        writer.WriteStringValue(pos);
        writer.WriteEndArray();
    }
}
