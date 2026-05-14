using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using AutoGo.Enums;
using AutoGo.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AutoGo.Views.Components;

public class GoBoardControl : Control
{
    public static readonly StyledProperty<EStoneType[,]> BoardDataProperty =
        AvaloniaProperty.Register<GoBoardControl, EStoneType[,]>(nameof(BoardData), new EStoneType[19, 19]);

    public static readonly StyledProperty<EStoneType?> CurrentPlayerProperty =
        AvaloniaProperty.Register<GoBoardControl, EStoneType?>(nameof(CurrentPlayer));

    public static readonly StyledProperty<ICommand?> PointClickedCommandProperty =
        AvaloniaProperty.Register<GoBoardControl, ICommand?>(nameof(PointClickedCommand));

    public static readonly StyledProperty<List<AnalysisPoint>?> AnalysisPointsProperty =
        AvaloniaProperty.Register<GoBoardControl, List<AnalysisPoint>?>(nameof(AnalysisPoints));

    private BoardCoords? _hoverCoords;

    static GoBoardControl()
    {
        AffectsRender<GoBoardControl>(BoardDataProperty, CurrentPlayerProperty, AnalysisPointsProperty);
    }

    public EStoneType[,] BoardData
    {
        get => GetValue(BoardDataProperty);
        set => SetValue(BoardDataProperty, value);
    }

    public EStoneType? CurrentPlayer
    {
        get => GetValue(CurrentPlayerProperty);
        set => SetValue(CurrentPlayerProperty, value);
    }

    public ICommand? PointClickedCommand
    {
        get => GetValue(PointClickedCommandProperty);
        set => SetValue(PointClickedCommandProperty, value);
    }

    public List<AnalysisPoint>? AnalysisPoints
    {
        get => GetValue(AnalysisPointsProperty);
        set => SetValue(AnalysisPointsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var boardSize = Math.Min(bounds.Width, bounds.Height);
        var originX = (bounds.Width - boardSize) / 2;
        var originY = (bounds.Height - boardSize) / 2;
        var origin = new Point(originX, originY);
        var step = boardSize / 20;

        context.FillRectangle(Brushes.White, new Rect(bounds.Size));

        var themePenBrush = Brushes.Black;
        var linePen = new Pen(themePenBrush);

        var geo = new BoardGeometry(boardSize, boardSize);

        for (var i = 0; i < 19; i++)
        {
            // (i, 0) to (i, 18) line
            var startH = Offset(geo.GetPixel(0, i));
            var endH = Offset(geo.GetPixel(18, i));
            context.DrawLine(linePen, startH, endH);
            // (0, i) to (18, i) line
            var startV = Offset(geo.GetPixel(i, 0));
            var endV = Offset(geo.GetPixel(i, 18));
            context.DrawLine(linePen, startV, endV);
        }

        // Draw star points
        var starPoints = new[] { 3, 9, 15 };
        foreach (var sp1 in starPoints)
        {
            foreach (var sp2 in starPoints)
            {
                var center = Offset(geo.GetPixel(sp1, sp2));
                context.DrawEllipse(themePenBrush, null, center, step * 0.15, step * 0.15);
            }
        }

        for (var x = 0; x < 19; x++)
        {
            if (x >= BoardData.GetLength(0))
            {
                continue;
            }
            for (var y = 0; y < 19; y++)
            {
                if (y >= BoardData.GetLength(1))
                {
                    continue;
                }
                var p = BoardData[x, y];
                if (p == EStoneType.None)
                {
                    continue;
                }

                var brush = p == EStoneType.Black ? Brushes.Black : Brushes.White;
                var center = Offset(geo.GetPixel(x, y));
                context.DrawEllipse(brush, linePen, center, step * 0.45, step * 0.45);
            }
        }

        MoveRecord? hoverMove = null;
        if (CurrentPlayer is not null
            && _hoverCoords is { X: var hx, Y: var hy }
            && BoardData.GetLength(0) > hx && BoardData.GetLength(1) > hy
            && BoardData[hx, hy] == EStoneType.None)
        {
            hoverMove = new MoveRecord(CurrentPlayer.Value, _hoverCoords);
        }

        if (AnalysisPoints is { Count: > 0 })
        {
            if (hoverMove is not null
                && AnalysisPoints.FirstOrDefault(p => p.Coords == hoverMove.Coords) is
                    { VariationPath.Count: > 0 } hoverAnalysisPoint)
            {
                var stepIndex = 1;
                var pathColor = CurrentPlayer == EStoneType.Black ? Colors.Black : Colors.White;
                // Skip current analysis point since it's the same as hover move
                foreach (var path in hoverAnalysisPoint.VariationPath)
                {
                    var center = Offset(geo.GetPixel(path.X, path.Y));
                    var radius = geo.GridSize * 0.4;
                    using (context.PushOpacity(0.6))
                    {
                        context.DrawEllipse(new SolidColorBrush(pathColor), linePen, center, radius, radius);
                    }
                    var formattedText = new FormattedText(
                        stepIndex.ToString(),
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(FontManager.Current.DefaultFontFamily),
                        16,
                        new SolidColorBrush(pathColor == Colors.Black ? Colors.White : Colors.Black)
                    );
                    context.DrawText(formattedText,
                        new Point(center.X - formattedText.Width / 2, center.Y - formattedText.Height / 2));
                    stepIndex += 1;
                    pathColor = pathColor == Colors.Black ? Colors.White : Colors.Black;
                }
            }
            else
            {
                foreach (var ap in AnalysisPoints)
                {
                    var apColor = ap.ThemeColor;
                    var center = Offset(geo.GetPixel(ap.Coords.X, ap.Coords.Y));
                    var radius = geo.GridSize * 0.4;
                    var ellipseLinePen = linePen;
                    if (AnalysisPoints.First() == ap)
                    {
                        // Highlight the best move
                        ellipseLinePen = new Pen(Brushes.Purple, 2);
                    }
                    using (context.PushOpacity(ap.Opacity))
                    {
                        context.DrawEllipse(new SolidColorBrush(apColor), ellipseLinePen, center, radius, radius);
                    }
                    // Draw ScoreLabel and WinRate
                    var textColor = Color.FromArgb(
                        (byte)(ap.Opacity * 255),
                        (byte)(apColor.R * 0.7),
                        (byte)(apColor.G * 0.7),
                        (byte)(apColor.B * 0.7)
                    );
                    if (!string.IsNullOrEmpty(ap.ScoreLabel))
                    {
                        var formattedText = new FormattedText(
                            ap.ScoreLabel,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(FontManager.Current.DefaultFontFamily),
                            10,
                            new SolidColorBrush(textColor)
                        );
                        context.DrawText(formattedText,
                            new Point(center.X - formattedText.Width / 2, center.Y - formattedText.Height));
                    }
                    if (ap.WinRate is >= 0 and <= 1)
                    {
                        var formattedText = new FormattedText(
                            ap.WinRate.ToString("P1"),
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(FontManager.Current.DefaultFontFamily),
                            10,
                            new SolidColorBrush(textColor)
                        );
                        context.DrawText(formattedText, new Point(center.X - formattedText.Width / 2, center.Y));
                    }
                }
            }
        }

        if (hoverMove is not null)
        {
            var center = Offset(geo.GetPixel(hoverMove.Coords!.Value.X, hoverMove.Coords!.Value.Y));
            var radius = geo.GridSize * 0.45;
            var color = CurrentPlayer == EStoneType.Black ? Colors.Black : Colors.White;
            using (context.PushOpacity(0.4))
            {
                context.DrawEllipse(new SolidColorBrush(color), linePen, center, radius, radius);
            }
        }
        return;

        Point Offset(Point point)
        {
            return new Point(point.X + origin.X, point.Y + origin.Y);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.Pointer.IsPrimary)
        {
            return;
        }

        var pixelPos = e.GetCurrentPoint(this).Position;
        var geo = CreateBoardGeometry(pixelPos, out var localPos);

        if (geo?.GetLogical(localPos) is { } coords)
        {
            PointClickedCommand?.Execute(coords);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pixelPos = e.GetCurrentPoint(this).Position;
        var geo = CreateBoardGeometry(pixelPos, out var localPos);
        var newHoverCoords = geo?.GetLogical(localPos);
        if (_hoverCoords != newHoverCoords)
        {
            _hoverCoords = newHoverCoords;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverCoords = null;
        InvalidateVisual();
    }

    private BoardGeometry? CreateBoardGeometry(Point pixelPos, out Point localPos)
    {
        var bounds = Bounds;
        var boardSize = Math.Min(bounds.Width, bounds.Height);
        var originX = (bounds.Width - boardSize) / 2;
        var originY = (bounds.Height - boardSize) / 2;

        localPos = new Point(pixelPos.X - originX, pixelPos.Y - originY);
        if (localPos.X < 0 || localPos.Y < 0 || localPos.X > boardSize || localPos.Y > boardSize)
        {
            return null;
        }

        return new BoardGeometry(boardSize, boardSize);
    }

    public class BoardGeometry(double width, double height, double padding = 20)
    {
        public double Padding { get; } = padding;
        public double GridSize { get; } = (Math.Min(width, height) - padding * 2) / 18.0;
        public double Width { get; } = width;
        public double Height { get; } = height;

        public Point GetPixel(int x, int y)
        {
            return new Point(
                Padding + x * GridSize,
                Padding + y * GridSize
            );
        }

        public BoardCoords? GetLogical(Point pixelPos)
        {
            var lx = (pixelPos.X - Padding) / GridSize;
            var ly = (pixelPos.Y - Padding) / GridSize;

            var ix = (int)Math.Round(lx);
            var iy = (int)Math.Round(ly);

            if (ix < 0 || ix >= 19 || iy < 0 || iy >= 19)
            {
                return null;
            }
            var dx = lx - ix;
            var dy = ly - iy;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.45)
            {
                return new(ix, iy);
            }

            return null;
        }
    }
}
