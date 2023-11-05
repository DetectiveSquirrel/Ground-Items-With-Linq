using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;
using SharpDX;
using System.Collections.Generic;
using System.Numerics;

namespace Ground_Items_With_Linq
{
    public class Ground_Items_With_LinqSettings : ISettings
    {
        //Mandatory setting to allow enabling/disabling your plugin
        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        public List<GroundRule> GroundRules { get; set; } = new List<GroundRule>();
        public RangeNode<int> UpdateTimer { get; set; } = new RangeNode<int>(500, 0, 5000);

        public ToggleNode EnableTextDrawing { get; set; } = new ToggleNode(true);
        public RangeNode<int> TextPadding { get; set; } = new RangeNode<int>(5, 0, 60);
        public RangeNode<int> RulesLocationX { get; set; } = new RangeNode<int>(800, 0, 2560);
        public RangeNode<int> RulesLocationY { get; set; } = new RangeNode<int>(800, 0, 1440);

        public ToggleNode EnableMapDrawing { get; set; } = new ToggleNode(true);
        public ColorNode MapLineColor { get; set; } = new ColorNode(new Color(214, 0, 255, 255));
        public RangeNode<float> MapLineThickness { get; set; } = new RangeNode<float>(2.317f, 1f, 10f);

        [JsonIgnore]
        public ButtonNode ReloadFilters { get; set; } = new ButtonNode();
    }
}

public class GroundRule
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public bool Enabled { get; set; } = false;
    public GroundRule(string name, string location, bool enabled)
    {
        Name = name;
        Location = location;
        Enabled = enabled;
    }
}