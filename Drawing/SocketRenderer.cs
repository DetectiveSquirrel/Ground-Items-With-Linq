using System.Collections.Generic;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq.Drawing;

public class SocketRenderer
{
    public enum Direction
    {
        None,
        Right,
        Down,
        Left
    }

    private static readonly Dictionary<int, Direction?> NormalSocketDirections = new()
    {
        [0] = Direction.Right,
        [1] = Direction.Down,
        [2] = Direction.Left,
        [3] = Direction.Down,
        [4] = Direction.Right
    };

    private static readonly Dictionary<int, Direction?> OneHandedSocketDirections = new()
    {
        [0] = Direction.Down,
        [1] = Direction.Down
    };

    public void RenderSockets(IEnumerable<string> socketGroups, Vector2N startingPoint, bool oneHander)
    {
        var socketDisplaySettings = Main.Settings.SocketDisplaySettings;
        var socketPosDiff = socketDisplaySettings.SocketSize + socketDisplaySettings.SocketSpacing;
        var sockets = new List<Socket>();
        var currentPosition = Vector2N.Zero;
        var socketIndex = 0;

        foreach (var socketItem in socketGroups)
            for (var charIndex = 0; charIndex < socketItem.Length; charIndex++)
            {
                var charColor = socketItem[charIndex];
                var trueDirection = GetSocketDirection(socketIndex, oneHander);
                var drawDirection = charIndex == socketItem.Length - 1 ? Direction.None : trueDirection;

                var socket = new Socket(
                    GetSocketColor(charColor),
                    currentPosition,
                    drawDirection,
                    socketDisplaySettings.LinkColor
                );

                currentPosition += GetPositionOffset(trueDirection, socketPosDiff);
                sockets.Add(socket);
                socketIndex++;
            }

        LinkSockets(sockets);
        RenderSocketChain(sockets, socketDisplaySettings, startingPoint);
    }

    private Direction GetSocketDirection(int index, bool oneHander)
    {
        return (oneHander ? OneHandedSocketDirections : NormalSocketDirections)
            .GetValueOrDefault(index) ?? Direction.None;
    }

    private Vector2N GetPositionOffset(Direction direction, float posDiff)
    {
        return direction switch
        {
            Direction.Right => Vector2N.UnitX * posDiff,
            Direction.Down => Vector2N.UnitY * posDiff,
            Direction.Left => -Vector2N.UnitX * posDiff,
            _ => Vector2N.Zero
        };
    }

    private Color GetSocketColor(char charColor)
    {
        return charColor switch
        {
            'R' => (Color)Main.Settings.SocketDisplaySettings.RedSocketColor,
            'G' => (Color)Main.Settings.SocketDisplaySettings.GreenSocketColor,
            'B' => (Color)Main.Settings.SocketDisplaySettings.BlueSocketColor,
            'W' => (Color)Main.Settings.SocketDisplaySettings.WhiteSocketColor,
            'A' => (Color)Main.Settings.SocketDisplaySettings.AbyssalSocketColor,
            'O' => (Color)Main.Settings.SocketDisplaySettings.ResonatorSocketColor,
            _ => Color.Black
        };
    }

    private void LinkSockets(IReadOnlyList<Socket> sockets)
    {
        for (var i = 0; i < sockets.Count - 1; i++) sockets[i].Link = sockets[i + 1];
    }

    private void RenderSocketChain(
        IEnumerable<Socket> sockets,
        SocketDisplaySettings settings,
        Vector2N startingPoint)
    {
        var socketSize = new Vector2N(settings.SocketSize, settings.SocketSize);
        foreach (var socket in sockets) socket.Draw(socketSize, settings.LinkWidth, startingPoint);
    }

    public record Socket(Color Color, Vector2N Position, Direction Direction, Color LinkColor)
    {
        public Socket Link { get; set; }

        public void Draw(Vector2N boxSize, float linkWidth, Vector2N startDrawLocation)
        {
            var drawPosition = startDrawLocation + Position;

            DrawLineToNextSocketIfPresent(boxSize, startDrawLocation, drawPosition, linkWidth);
            DrawBoxAtPosition(boxSize, drawPosition);
        }

        private void DrawBoxAtPosition(Vector2N boxSize, Vector2N drawPosition)
        {
            Main.Graphics.DrawBox(new RectangleF(drawPosition.X, drawPosition.Y, boxSize.X, boxSize.Y), Color);
        }

        private void DrawLineToNextSocketIfPresent(Vector2N boxSize, Vector2N startDrawLocation, Vector2N drawPosition,
            float linkWidth)
        {
            if (Link == null) return;

            if (Direction != Direction.None)
                Main.Graphics.DrawLine(
                    drawPosition + boxSize / 2,
                    startDrawLocation + Link.Position + boxSize / 2,
                    linkWidth,
                    LinkColor
                );
        }
    }
}