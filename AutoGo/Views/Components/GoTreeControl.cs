using System;
using System.Collections.Generic;
using System.Linq;
using AutoGo.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AutoGo.Views.Components;

public class GoTreeControl : Control
{
    private const int XStep = 30;
    private const int YStep = 30;
    private const int Padding = 10;
    private const int NodeRadius = 5;
    private static readonly Pen EdgePen = new(Brushes.Black, 1);

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, GameStateNode>> GameStateNodesProperty =
        AvaloniaProperty.Register<GoTreeControl, IReadOnlyDictionary<Guid, GameStateNode>>(nameof(GameStateNodes));

    public static readonly StyledProperty<GameStateNode> SelectedNodeProperty =
        AvaloniaProperty.Register<GoTreeControl, GameStateNode>(nameof(SelectedNode));

    static GoTreeControl()
    {
        AffectsRender<GoTreeControl>(GameStateNodesProperty, SelectedNodeProperty);
    }

    public IReadOnlyDictionary<Guid, GameStateNode> GameStateNodes
    {
        get => GetValue(GameStateNodesProperty);
        set => SetValue(GameStateNodesProperty, value);
    }

    public GameStateNode SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(Brushes.White, new Rect(bounds.Size));
        var selectedNode = SelectedNode;
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
        if (!GameStateNodes.ContainsKey(selectedNode.Id))
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
        var selectedWorldX = (double)selectedNodePosition.Item1 * XStep + Padding;
        var selectedWorldY = (double)selectedNodePosition.Item2 * YStep + Padding;
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
        var cullingRect = new Rect(0, 0, bounds.Width, bounds.Height)
            .Inflate(NodeRadius + 1);

        foreach (var node in GameStateNodes.Values)
        {
            if (!nodePositionsById.TryGetValue(node.Id, out var nodePosition))
            {
                continue;
            }

            var nodePoint = ToScreenPoint(nodePosition, xPixelOffset, yPixelOffset);

            foreach (var childId in node.ChildrenIds)
            {
                if (!nodePositionsById.TryGetValue(childId, out var childPosition))
                {
                    continue;
                }

                var childPoint = ToScreenPoint(childPosition, xPixelOffset, yPixelOffset);
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

            var nodePoint = ToScreenPoint(nodePosition, xPixelOffset, yPixelOffset);
            var nodeRect = new Rect(
                nodePoint.X - NodeRadius,
                nodePoint.Y - NodeRadius,
                NodeRadius * 2,
                NodeRadius * 2);
            if (!nodeRect.Intersects(cullingRect))
            {
                continue;
            }

            context.DrawEllipse(node.Id == SelectedNode.Id ? Brushes.Red : Brushes.Black, null, nodeRect);
        }
    }

    private static Point ToScreenPoint((int X, int Y) position, double xPixelOffset, double yPixelOffset)
    {
        return new Point(position.X * XStep + Padding + xPixelOffset, position.Y * YStep + Padding + yPixelOffset);
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
