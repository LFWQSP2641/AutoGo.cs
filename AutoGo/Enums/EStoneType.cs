using System;

namespace AutoGo.Enums;

public enum EStoneType
{
    None = 0,
    Black = 1,
    White = 2,
}

public static class EStoneTypeConverter
{
    public static EStoneType FromGtpColor(string gtpColor)
    {
        return gtpColor.ToUpper() switch
        {
            "B" => EStoneType.Black,
            "W" => EStoneType.White,
            "BLACK" => EStoneType.Black,
            "WHITE" => EStoneType.White,
            _ => throw new ArgumentException($"Invalid GTP color: {gtpColor}"),
        };
    }
}
