using System;
using AutoGo.Enums;

namespace AutoGo.Common;

public static class Extension
{
    public static string ToGtpColor(this EStoneType stoneType)
    {
        return stoneType switch
        {
            EStoneType.Black => "B",
            EStoneType.White => "W",
            _ => throw new ArgumentException("Invalid stone type"),
        };
    }
}
