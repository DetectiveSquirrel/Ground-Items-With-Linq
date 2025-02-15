using System;
using System.Collections.Generic;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq.Drawing;

public class DrawingLabels
{
    private static readonly SocketRenderer SocketRenderer = new();

    public static void RenderItemsOnScreen(List<CustomItemData> wantedItems)
    {
        var playerPos = Main.GameController.Player.GridPosNum;
        var position = Main.GameController.UnderPanel.StartDrawPointNum;
        position.X += Main.Settings.LabelShift;

        var defaultAlertDrawStyle = new AlertDrawStyle(
            "<SOMETHINGS WRONG>",
            Color.White,
            Main.Settings.BorderWidth,
            Color.White,
            Color.Black
        );

        if (Main.Settings.EnableTextDrawing)
            foreach (var entity in wantedItems)
            {
                var text = entity.UniqueNameCandidates.Count != 0
                    ? string.Join(" \\\n", entity.UniqueNameCandidates)
                    : entity.LabelText;

                var alertDrawStyle = defaultAlertDrawStyle with
                {
                    Text = text,
                    TextColor = entity.TextColor,
                    BackgroundColor = entity.BackgroundColor,
                    BorderColor = entity.BorderColor
                };

                position = RenderItemLabel(
                    playerPos,
                    position,
                    Main.Settings.ItemSpacing,
                    alertDrawStyle,
                    entity
                );
            }

        if (!Main.Settings.EnableMapDrawing || !Main.LargeMap.IsVisible) return;

        foreach (var item in wantedItems)
            Main.Graphics.DrawLine(
                Main.GameController.IngameState.Data.GetGridMapScreenPosition(item.Location),
                Main.GameController.IngameState.Data.GetGridMapScreenPosition(Main.GameController.Player.GridPosNum),
                Main.Settings.MapLineThickness,
                Main.Settings.MapLineColor
            );
    }

    public static Vector2N RenderItemLabel(Vector2N playerPos, Vector2N position, float bottomMargin,
        AlertDrawStyle drawStyle, CustomItemData entity)
    {
        var delta = entity.Location - playerPos;
        var itemSize = RenderLabelWithSocket(drawStyle, delta, position, drawStyle.Text, entity);

        if (itemSize != 0) position.Y += itemSize + bottomMargin;

        return position;
    }

    private static float RenderLabelWithSocket(AlertDrawStyle drawStyle, Vector2N delta, Vector2N position, string text,
        CustomItemData entity)
    {
        var padding = Main.Settings.TextPadding.Value;
        var compassOffset = Main.Settings.DrawCompass ? Main.Settings.TextSize * ImGui.GetFontSize() * 2 : 0;
        position += new Vector2N(-drawStyle.BorderWidth - compassOffset, drawStyle.BorderWidth);
        var distance = delta.GetPolarCoordinates(out var phi);
        var sockets = entity.SocketInfo.SocketNumber;
        float singleRowTextHeight;

        using (Main.Graphics.SetTextScale(Main.Settings.TextSize))
        {
            singleRowTextHeight = Main.Graphics.MeasureText("aAyY").Y;
        }

        var enableSocketDisplay = sockets > 0 && Main.Settings.SocketDisplaySettings.ShowSockets;

        int socketWidth;
        int socketHeight;
        int socketPadding;

        if (enableSocketDisplay)
        {
            int NSocketSpace(int socketCount)
            {
                return Main.Settings.SocketDisplaySettings.SocketSize * socketCount +
                       Main.Settings.SocketDisplaySettings.SocketSpacing * (socketCount - 1);
            }

            socketPadding = Main.Settings.SocketDisplaySettings.SocketPadding;
            if (entity.Width == 1 || sockets == 1)
            {
                socketWidth = NSocketSpace(1);
                socketHeight = NSocketSpace(sockets);
            }
            else
            {
                socketWidth = NSocketSpace(2);
                socketHeight = sockets switch
                {
                    < 3 => NSocketSpace(1),
                    < 5 => NSocketSpace(2),
                    _ => NSocketSpace(3)
                };
            }
        }
        else
        {
            socketHeight = 0;
            socketWidth = 0;
            socketPadding = 0;
        }

        var isDefaultFont = string.IsNullOrEmpty(Main.Settings.FontOverride.Value);
        var baseTextSize = isDefaultFont
            ? Main.Graphics.MeasureText(text)
            : Main.Graphics.MeasureText(text, Main.Settings.FontOverride.Value);
        float actualTextScale = isDefaultFont || Main.Settings.ScaleFontWhenCustom
            ? Main.Settings.TextSize
            : 1;
        var textSize = baseTextSize * actualTextScale;

        var socketAreaWidth = socketWidth +
                              (enableSocketDisplay ? Math.Max(padding.X, socketPadding) + socketPadding : padding.X);
        var fullWidth = textSize.X + padding.X + socketAreaWidth;
        var textHeightWithPadding = textSize.Y + padding.Y * 2;
        var fullHeight = Math.Max(textHeightWithPadding, socketHeight + socketPadding * 2);

        var boxRect = new RectangleF(position.X - fullWidth, position.Y, fullWidth, fullHeight);
        Main.Graphics.DrawBox(boxRect, drawStyle.BackgroundColor);
        if (drawStyle.BorderWidth > 0)
        {
            var frameRect = boxRect;
            frameRect.Inflate(drawStyle.BorderWidth, drawStyle.BorderWidth);
            Main.Graphics.DrawFrame(frameRect, drawStyle.BorderColor, drawStyle.BorderWidth);
        }

        using (Main.Graphics.SetTextScale(actualTextScale))
        {
            var textPos = position + new Vector2N(-socketAreaWidth, fullHeight / 2 - textSize.Y / 2);

            float DrawLine(string line, Vector2N pos)
            {
                return isDefaultFont
                    ? Main.Graphics.DrawText(line, pos, drawStyle.TextColor, FontAlign.Right).Y
                    : Main.Graphics.DrawText(line, pos, drawStyle.TextColor, Main.Settings.FontOverride.Value,
                        FontAlign.Right).Y;
            }

            if (Main.Settings.AlignItemTextToCenter)
                foreach (var line in text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var lineSize = isDefaultFont
                        ? Main.Graphics.MeasureText(line)
                        : Main.Graphics.MeasureText(line, Main.Settings.FontOverride.Value);
                    textPos.Y += DrawLine(line, textPos + new Vector2N(-textSize.X / 2 + lineSize.X / 2, 0));
                }
            else
                DrawLine(text, textPos);
        }

        if (Main.Settings.DrawCompass)
        {
            var compassUv = MathHepler.GetDirectionsUV(phi, distance);
            var compassRect = new RectangleF(
                position.X + drawStyle.BorderWidth + compassOffset / 2 - singleRowTextHeight / 2,
                Main.Settings.AlignCompassToCenter ? boxRect.Center.Y - singleRowTextHeight / 2 : boxRect.Top,
                singleRowTextHeight,
                singleRowTextHeight
            );

            Main.Graphics.DrawImage("directions.png", compassRect, compassUv);
        }

        if (enableSocketDisplay)
        {
            var socketStartingPoint = new Vector2N(
                boxRect.Right - socketWidth - socketPadding,
                boxRect.Center.Y - socketHeight / 2f
            );

            SocketRenderer.RenderSockets(entity.SocketInfo.SocketGroups, socketStartingPoint, entity.Width == 1);
        }

        return fullHeight + drawStyle.BorderWidth * 2;
    }
}