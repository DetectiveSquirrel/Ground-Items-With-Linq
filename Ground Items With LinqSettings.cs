using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;
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
        public RangeNode<int> RulesLocationX { get; set; } = new RangeNode<int>(800, 0, 2560);
        public RangeNode<int> RulesLocationY { get; set; } = new RangeNode<int>(800, 0, 1440);

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