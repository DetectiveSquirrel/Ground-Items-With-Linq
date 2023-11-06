using Color = SharpDX.Color;

namespace Ground_Items_With_Linq;

public record AlertDrawStyle
{
    public AlertDrawStyle(string text, Color textColor, int borderWidth, Color borderColor, Color backgroundColor)
    {
        TextColor = textColor;
        BorderWidth = borderWidth;
        BorderColor = borderColor;
        Text = text;
        BackgroundColor = backgroundColor;
    }

    public Color TextColor { get; set; }
    public int BorderWidth { get; set; }
    public Color BorderColor { get; set; }
    public Color BackgroundColor { get; set; }
    public string Text { get; set; }
    public int IconIndex { get; set; }
}