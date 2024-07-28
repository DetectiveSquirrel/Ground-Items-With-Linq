using Color = SharpDX.Color;

namespace Ground_Items_With_Linq;

public record AlertDrawStyle(string Text, int TextSize, float LabelScale, Color TextColor, int BorderWidth, Color BorderColor, Color BackgroundColor);