using System.Text.Json;
using AutoGo.Common;
using AutoGo.Common.Json;
using AutoGo.Enums;
using AutoGo.Models;
using AwesomeAssertions;

namespace AutoGo.Tests;

public class GtpConverterTests
{
    public static TheoryData<EStoneType, string> ColorTestData => new()
    {
        { EStoneType.Black, "B" }, { EStoneType.White, "W" },
    };

    public static TheoryData<(int, int), string, int> VertexTestData => new()
    {
        { (15, 15), "Q4", 19 },
        { (0, 18), "A1", 19 },
        { (18, 0), "T19", 19 },
        { (3, 3), "D16", 19 },
        // Board size 9
        { (8, 8), "J1", 9 },
        { (0, 0), "A9", 9 },
        // Board size 13
        { (11, 12), "M1", 13 },
        { (0, 0), "A13", 13 },
    };

    public static TheoryData<EStoneType, (int, int)?, string> MoveConverterTestData => new()
    {
        // Black moves
        { EStoneType.Black, (15, 15), "[\"B\",\"Q4\"]" },
        { EStoneType.Black, (0, 18), "[\"B\",\"A1\"]" },
        { EStoneType.Black, (18, 0), "[\"B\",\"T19\"]" },
        // White moves
        { EStoneType.White, (3, 3), "[\"W\",\"D16\"]" },
        { EStoneType.White, (15, 15), "[\"W\",\"Q4\"]" },
        // Pass moves
        { EStoneType.Black, null, "[\"B\",\"pass\"]" },
        { EStoneType.White, null, "[\"W\",\"pass\"]" },
    };

    [Theory]
    [MemberData(nameof(ColorTestData))]
    public void TestColorConverter(EStoneType expected, string gtpColor)
    {
        var result = expected.ToGtpColor();
        result.Should().Be(gtpColor);
    }

    [Theory]
    [MemberData(nameof(ColorTestData))]
    public void TestColorParser(EStoneType stoneType, string expected)
    {
        var result = EStoneTypeConverter.FromGtpColor(expected);
        result.Should().Be(stoneType);
    }

    [Theory]
    [MemberData(nameof(VertexTestData))]
    public void TestVertexParser((int, int) expectedCoords, string gtpVertex, int boardSize)
    {
        var result = BoardCoords.ParseGtpVertex(gtpVertex, boardSize);
        result.Should().Be(new BoardCoords(expectedCoords.Item1, expectedCoords.Item2));
    }

    [Theory]
    [MemberData(nameof(VertexTestData))]
    public void TestVertexConverter((int, int) coords, string expected, int boardSize)
    {
        var result = new BoardCoords(coords.Item1, coords.Item2).ToGtpVertex(boardSize);
        result.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(MoveConverterTestData))]
    public void TestMoveConverterWrite(EStoneType stoneType, (int, int)? xy, string expected)
    {
        BoardCoords? coords = xy is var (x, y) ? new BoardCoords(x, y) : null;
        var move = new MoveRecord(stoneType, coords);
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };
        var result = JsonSerializer.Serialize(move, options);
        result.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(MoveConverterTestData))]
    public void TestMoveConverterRead(EStoneType expectedStoneType, (int, int)? expectedXy, string json)
    {
        BoardCoords? expectedCoords = expectedXy is var (x, y) ? new BoardCoords(x, y) : null;
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };
        var result = JsonSerializer.Deserialize<MoveRecord>(json, options);
        result.Should().NotBeNull();
        result.StoneType.Should().Be(expectedStoneType);
        result.Coords.Should().Be(expectedCoords);
    }

    [Fact]
    public void TestMoveConverterRoundTrip()
    {
        var move = new MoveRecord(EStoneType.Black, new BoardCoords(10, 10));
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };

        var json = JsonSerializer.Serialize(move, options);
        var deserialized = JsonSerializer.Deserialize<MoveRecord>(json, options);

        deserialized.Should().Be(move);
    }

    [Fact]
    public void TestMoveConverterPassRoundTrip()
    {
        var move = new MoveRecord(EStoneType.White, null);
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };

        var json = JsonSerializer.Serialize(move, options);
        var deserialized = JsonSerializer.Deserialize<MoveRecord>(json, options);

        deserialized.Should().Be(move);
        deserialized.IsPass.Should().BeTrue();
    }

    [Fact]
    public void TestMoveConverterInvalidColorThrows()
    {
        const string invalidJson = "[\"X\",\"Q4\"]";
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };

        var act = () => JsonSerializer.Deserialize<MoveRecord>(invalidJson, options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TestMoveConverterMissingColorThrows()
    {
        const string invalidJson = "[\"Q4\"]";
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };

        var act = () => JsonSerializer.Deserialize<MoveRecord>(invalidJson, options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void TestMoveConverterTooManyElementsThrows()
    {
        const string invalidJson = "[\"B\",\"Q4\",\"extra\"]";
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };

        var act = () => JsonSerializer.Deserialize<MoveRecord>(invalidJson, options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void TestMoveConverterNotArrayThrows()
    {
        const string invalidJson = "{\"color\":\"B\",\"position\":\"Q4\"}";
        var options = new JsonSerializerOptions { Converters = { new MoveConverter() } };

        var act = () => JsonSerializer.Deserialize<MoveRecord>(invalidJson, options);
        act.Should().Throw<JsonException>();
    }
}
