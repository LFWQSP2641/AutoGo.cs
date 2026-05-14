using System;

namespace AutoGo.Models;

public record struct BoardCoords(int X, int Y)
{
    public static BoardCoords? ParseGtpVertex(string vertex, int boardSize = 19)
    {
        if (vertex.Length < 2)
        {
            return null;
        }
        if (vertex.Equals("pass", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var columnChar = vertex[0];
        var rowStr = vertex[1..];

        if (!char.IsLetter(columnChar) || !int.TryParse(rowStr, out var row))
        {
            return null;
        }

        // Convert column char to 0-based index (A=0, B=1, ..., T=18, skipping I)
        var column = char.ToUpper(columnChar) - 'A';
        if (column >= 8) // Skip 'I'
        {
            column--;
        }

        // GTP row 1 is bottom, so convert to 0-based top-left origin
        var y = boardSize - row;

        return new BoardCoords(column, y);
    }

    public string ToGtpVertex(int boardSize = 19)
    {
        // Convert X to GTP column letter (A–T, skipping I)
        var col = (char)('A' + (X >= 8 ? X + 1 : X));

        // Convert Y (0 = top) to GTP row (1 = bottom)
        var row = boardSize - Y;

        return $"{col}{row}";
    }
}
