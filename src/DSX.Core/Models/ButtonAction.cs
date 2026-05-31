using System.Collections.Generic;
using DSX.Core.Enums;

namespace DSX.Core.Models;

public class ButtonAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ControllerButton Button { get; set; }
    public PressType PressType { get; set; } = PressType.SinglePress;
    public ActionType ActionType { get; set; } = ActionType.KeyboardKey;
    public string KeyValue { get; set; } = string.Empty;
    public MouseButton MouseButton { get; set; }
    public MouseScrollDirection ScrollDirection { get; set; }
    public ControllerButton RemapTarget { get; set; }
    public string ApplicationPath { get; set; } = string.Empty;
    public string TargetProfileId { get; set; } = string.Empty;
    public List<MacroStep> MacroSteps { get; set; } = new();
    public double HoldDurationMs { get; set; } = 300;
    public double DoublePressWindowMs { get; set; } = 300;
    public bool IsEnabled { get; set; } = true;
}

public class MacroStep
{
    public ActionType ActionType { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DelayMs { get; set; } = 50;
}
