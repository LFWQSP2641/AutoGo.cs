using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AutoGo.Enums;
using AutoGo.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AutoGo.Views.Components;

public class GoTreeControl : Control
{
    private const int BaseXStep = 30;
    private const int BaseYStep = 30;
    private const int Padding = 10;
    private const int BaseNodeRadius = 5;
    private const double MinZoom = 0.4;
    private const double MaxZoom = 3.5;
    private const double ZoomFactor = 1.1;
    private const double ZoomEpsilon = 0.0001;
    private const double PreviewSize = 120;
    private const double PreviewPadding = 10;
    private const double PreviewOffset = 14;
    private const double PreviewBoundaryMargin = 4;
    private const double BoardGridIntervals = 18.0;
    private const double StoneRadiusFactor = 0.4;
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Pen EdgePen = new(Brushes.Black, 1);
    private static readonly Pen MiniBoardPen = new(Brushes.Black, 1);
    private static readonly Pen MiniStonePen = new(Brushes.Black, 1);
    private static readonly IBrush MiniBoardBackground = new SolidColorBrush(Color.Parse("#F2D39B"));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, GameStateNode>> GameStateNodesProperty =
        AvaloniaProperty.Register<GoTreeControl, IReadOnlyDictionary<Guid, GameStateNode>>(
            nameof(GameStateNodes), new Dictionary<Guid, GameStateNode>());

    public static readonly StyledProperty<GameStateNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<GoTreeControl, GameStateNode?>(nameof(SelectedNode));

    public static readonly StyledProperty<ICommand?> NodeClickedCommandProperty =
        AvaloniaProperty.Register<GoTreeControl, ICommand?>(nameof(NodeClickedCommand));

    private Dictionary<Guid, (int X, int Y)> _lastNodePositionsById = new();
    private double _lastXPixelOffset;
    private double _lastYPixelOffset;
    private double _lastXStep = BaseXStep;
    private double _lastYStep = BaseYStep;
    private double _lastNodeRadius = BaseNodeRadius;
    private double _zoom = 1.0;
    private Guid? _hoveredNodeId;
    private Point _lastPointerPosition;

    static GoTreeControl()
    {
        AffectsRender<GoTreeControl>(GameStateNodesProperty, SelectedNodeProperty);
    }

    public IReadOnlyDictionary<Guid, GameStateNode> GameStateNodes
    {
        get => GetValue(GameStateNodesProperty);
        set => SetValue(GameStateNodesProperty, value);
    }

    public GameStateNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public ICommand? NodeClickedCommand
    {
        get => GetValue(NodeClickedCommandProperty);
        set => SetValue(NodeClickedCommandProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(Brushes.White, new Rect(bounds.Size));
        var selectedNode = SelectedNode;
        var xStep = BaseXStep * _zoom;
        var yStep = BaseYStep * _zoom;
        var nodeRadius = Math.Max(3, BaseNodeRadius * _zoom);
        var longestPath = new List<GameStateNode>();
        foreach (var node in GameStateNodes.Where(n => n.Value.ChildrenIds.Count == 0))
        {
            var path = new List<GameStateNode>();
            var currentNode = node.Value;
            while (true)
            {
                path.Add(currentNode);
                if (currentNode.ParentId == null)
                {
                    break;
                }
                currentNode = GameStateNodes[currentNode.ParentId.Value];
            }
            if (path.Count > longestPath.Count)
            {
                longestPath = path;
            }
        }
        if (longestPath.Count == 0)
        {
            return;
        }
        longestPath = longestPath.AsEnumerable().Reverse().ToList();
        if (selectedNode is null || !GameStateNodes.ContainsKey(selectedNode.Id))
        {
            selectedNode = longestPath.Last();
        }
        // Define selected node position
        var targetX = bounds.Width / 2;
        var targetY = bounds.Height / 2;
        var nodePositions = new List<NodePosition>();
        var allRootNodes = GameStateNodes.Values.Where(n => n.ParentId == null).ToList();
        var placeholderMap = new Dictionary<int, int>();
        var yPositionOffset = 0;
        foreach (var layoutResult in allRootNodes.Select(node => CalculateNodePositions(node, 0, yPositionOffset, placeholderMap)))
        {
            nodePositions.AddRange(layoutResult.NodePositions);
            yPositionOffset = layoutResult.MaxY + 1;
        }
        var selectedNodePosition = nodePositions.FirstOrDefault(p => p.Node.Id == selectedNode.Id)?.Position ?? (0, 0);
        var selectedWorldX = selectedNodePosition.Item1 * xStep + Padding;
        var selectedWorldY = selectedNodePosition.Item2 * yStep + Padding;
        // clamp targetY
        var minY = (double)Padding;
        var maxY = bounds.Height - Padding;

        if (minY >= maxY)
        {
            minY = bounds.Height / 2;
            maxY = bounds.Height / 2;
        }

        targetY = Math.Clamp(targetY, minY, maxY);

        // offset
        var xPixelOffset = targetX - selectedWorldX;
        var yPixelOffset = targetY - selectedWorldY;

        var nodePositionsById = nodePositions.ToDictionary(p => p.Node.Id, p => p.Position);
        _lastNodePositionsById.Clear();
        foreach (var (nodeId, position) in nodePositionsById)
        {
            _lastNodePositionsById[nodeId] = position;
        }
        _lastXPixelOffset = xPixelOffset;
        _lastYPixelOffset = yPixelOffset;
        _lastXStep = xStep;
        _lastYStep = yStep;
        _lastNodeRadius = nodeRadius;
        var cullingRect = new Rect(0, 0, bounds.Width, bounds.Height)
            .Inflate(nodeRadius + 1);

        foreach (var node in GameStateNodes.Values)
        {
            if (!nodePositionsById.TryGetValue(node.Id, out var nodePosition))
            {
                continue;
            }

            var nodePoint = ToScreenPoint(nodePosition, xPixelOffset, yPixelOffset, xStep, yStep);

            foreach (var childId in node.ChildrenIds)
            {
                if (!nodePositionsById.TryGetValue(childId, out var childPosition))
                {
                    continue;
                }

                var childPoint = ToScreenPoint(childPosition, xPixelOffset, yPixelOffset, xStep, yStep);
                if (!IsEdgeVisible(cullingRect, nodePoint, childPoint))
                {
                    continue;
                }

                context.DrawLine(EdgePen, nodePoint, childPoint);
            }
        }

        foreach (var node in GameStateNodes.Values)
        {
            if (!nodePositionsById.TryGetValue(node.Id, out var nodePosition))
            {
                continue;
            }

            var nodePoint = ToScreenPoint(nodePosition, xPixelOffset, yPixelOffset, xStep, yStep);
            var nodeRect = new Rect(
                nodePoint.X - nodeRadius,
                nodePoint.Y - nodeRadius,
                nodeRadius * 2,
                nodeRadius * 2);
            if (!nodeRect.Intersects(cullingRect))
            {
                continue;
            }

            context.DrawEllipse(node.Id == selectedNode.Id ? Brushes.Red : Brushes.Black, null, nodeRect);
        }

        if (_hoveredNodeId is { } hoveredNodeId
            && GameStateNodes.TryGetValue(hoveredNodeId, out var hoveredNode))
        {
            DrawMiniBoardPreview(context, hoveredNode.BoardStateCache, bounds);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.Pointer.IsPrimary || _lastNodePositionsById.Count == 0)
        {
            return;
        }

        var clickPoint = e.GetCurrentPoint(this).Position;
        GameStateNode? clickedNode = null;
        var minDistanceSquared = double.MaxValue;
        var hitRadiusSquared = _lastNodeRadius * _lastNodeRadius;

        foreach (var (nodeId, position) in _lastNodePositionsById)
        {
            if (!GameStateNodes.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            var nodePoint = ToScreenPoint(position, _lastXPixelOffset, _lastYPixelOffset, _lastXStep, _lastYStep);
            var dx = nodePoint.X - clickPoint.X;
            var dy = nodePoint.Y - clickPoint.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > hitRadiusSquared || distanceSquared >= minDistanceSquared)
            {
                continue;
            }

            minDistanceSquared = distanceSquared;
            clickedNode = node;
        }

        if (clickedNode is null || !(NodeClickedCommand?.CanExecute(clickedNode) ?? false))
        {
            return;
        }

        NodeClickedCommand.Execute(clickedNode);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _lastPointerPosition = e.GetCurrentPoint(this).Position;

        var previousHovered = _hoveredNodeId;
        var hoveredNode = FindNodeAtPoint(_lastPointerPosition);
        _hoveredNodeId = hoveredNode?.Id;

        Cursor = hoveredNode is not null && (NodeClickedCommand?.CanExecute(hoveredNode) ?? false)
            ? HandCursor
            : ArrowCursor;

        if (previousHovered != _hoveredNodeId)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoveredNodeId = null;
        Cursor = ArrowCursor;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var deltaY = e.Delta.Y;
        if (deltaY == 0)
        {
            return;
        }

        var nextZoom = deltaY > 0 ? _zoom * ZoomFactor : _zoom / ZoomFactor;
        var clampedZoom = Math.Clamp(nextZoom, MinZoom, MaxZoom);
        if (Math.Abs(clampedZoom - _zoom) < ZoomEpsilon)
        {
            return;
        }

        _zoom = clampedZoom;
        InvalidateVisual();
        e.Handled = true;
    }

    private GameStateNode? FindNodeAtPoint(Point point)
    {
        if (_lastNodePositionsById.Count == 0)
        {
            return null;
        }

        GameStateNode? closestNode = null;
        var minDistanceSquared = double.MaxValue;
        var hitRadiusSquared = _lastNodeRadius * _lastNodeRadius;
        foreach (var (nodeId, position) in _lastNodePositionsById)
        {
            if (!GameStateNodes.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            var nodePoint = ToScreenPoint(position, _lastXPixelOffset, _lastYPixelOffset, _lastXStep, _lastYStep);
            var dx = nodePoint.X - point.X;
            var dy = nodePoint.Y - point.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > hitRadiusSquared || distanceSquared >= minDistanceSquared)
            {
                continue;
            }

            minDistanceSquared = distanceSquared;
            closestNode = node;
        }

        return closestNode;
    }

    private static Point ToScreenPoint((int X, int Y) position, double xPixelOffset, double yPixelOffset, double xStep,
        double yStep)
    {
        return new Point(position.X * xStep + Padding + xPixelOffset, position.Y * yStep + Padding + yPixelOffset);
    }

    private static bool IsEdgeVisible(Rect cullingRect, Point start, Point end)
    {
        if (cullingRect.Contains(start) || cullingRect.Contains(end))
        {
            return true;
        }

        var edgeRect = new Rect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(start.X - end.X),
            Math.Abs(start.Y - end.Y)).Inflate(1);
        return edgeRect.Intersects(cullingRect);
    }

    private void DrawMiniBoardPreview(DrawingContext context, EStoneType[,] boardState, Rect bounds)
    {
        var anchor = new Point(_lastPointerPosition.X + PreviewOffset, _lastPointerPosition.Y + PreviewOffset);
        var x = anchor.X;
        var y = anchor.Y;
        if (x + PreviewSize > bounds.Width - PreviewBoundaryMargin)
        {
            x = Math.Max(PreviewBoundaryMargin, _lastPointerPosition.X - PreviewSize - PreviewOffset);
        }
        if (y + PreviewSize > bounds.Height - PreviewBoundaryMargin)
        {
            y = Math.Max(PreviewBoundaryMargin, _lastPointerPosition.Y - PreviewSize - PreviewOffset);
        }

        var previewRect = new Rect(x, y, PreviewSize, PreviewSize);
        context.DrawRectangle(Brushes.White, MiniBoardPen, previewRect);

        var boardRect = new Rect(
            previewRect.X + PreviewPadding,
            previewRect.Y + PreviewPadding,
            previewRect.Width - PreviewPadding * 2,
            previewRect.Height - PreviewPadding * 2);
        context.DrawRectangle(MiniBoardBackground, MiniBoardPen, boardRect);

        var gridStep = boardRect.Width / BoardGridIntervals;
        for (var i = 0; i < 19; i++)
        {
            var xLine = boardRect.X + i * gridStep;
            var yLine = boardRect.Y + i * gridStep;
            context.DrawLine(MiniBoardPen, new Point(xLine, boardRect.Y), new Point(xLine, boardRect.Bottom));
            context.DrawLine(MiniBoardPen, new Point(boardRect.X, yLine), new Point(boardRect.Right, yLine));
        }

        var stoneRadius = gridStep * StoneRadiusFactor;
        for (var boardX = 0; boardX < 19; boardX++)
        {
            for (var boardY = 0; boardY < 19; boardY++)
            {
                var stone = boardState[boardX, boardY];
                if (stone == EStoneType.None)
                {
                    continue;
                }

                var center = new Point(boardRect.X + boardX * gridStep, boardRect.Y + boardY * gridStep);
                var brush = stone == EStoneType.Black ? Brushes.Black : Brushes.White;
                context.DrawEllipse(brush, MiniStonePen, center, stoneRadius, stoneRadius);
            }
        }
    }

    private LayoutResult CalculateNodePositions(GameStateNode rootNode, int xOffset, int yOffset,
        IDictionary<int, int> placeholderMap)
    {
        var positions = new List<NodePosition>();
        var longestPath = GameStateNode.GetLongestPath(rootNode, GameStateNodes);
        var xEnd = xOffset + longestPath.Count - 1;
        var y = yOffset;
        while (placeholderMap.TryGetValue(y, out var lastPlaceholderX)
               && lastPlaceholderX <= xEnd)
        {
            y += 1;
        }
        for (var i = yOffset; i <= y; i++)
        {
            // There is no cross in the tree, so we can use the same placeholder for all nodes in the same row
            placeholderMap[i] = xOffset;
        }

        var maxY = y;

        for (var i = 0; i < longestPath.Count; i++)
        {
            var position = (xOffset + i, y);
            positions.Add(new NodePosition { Node = longestPath[i], Position = position });
        }
        var forkIndices = longestPath
            .Select((n, i) => (Node: n, Index: i))
            .Where(t => t.Node.ChildrenIds.Count > 1)
            .Select(t => t.Index)
            .ToArray();
        for (var k = forkIndices.Length - 1; k >= 0; k--)
        {
            var forkIndex = forkIndices[k];
            var recursionNodes = longestPath[forkIndex] with
            {
                ChildrenIds = longestPath[forkIndex].ChildrenIds
                    .Where(id => id != longestPath[forkIndex + 1].Id).ToList(),
            };
            var xOffsetFork = xOffset + forkIndex;
            var yOffsetFork = y + 1;
            var forkLayoutResult = CalculateNodePositions(recursionNodes, xOffsetFork, yOffsetFork, placeholderMap);
            maxY = Math.Max(maxY, forkLayoutResult.MaxY);
            var forkPositions = forkLayoutResult.NodePositions;
            // skip the first node, which is the fork node itself
            for (var i = 1; i < forkPositions.Count; i++)
            {
                positions.Add(forkPositions[i]);
            }
        }
        return new LayoutResult
        {
            NodePositions = positions,
            MaxX = xEnd,
            MaxY = maxY,
        };
    }

    private record NodePosition
    {
        public required GameStateNode Node { get; init; }
        public (int, int) Position { get; init; }
    }

    private record LayoutResult
    {
        public required List<NodePosition> NodePositions { get; init; }
        public int MaxX { get; init; }
        public int MaxY { get; init; }
    }
}
