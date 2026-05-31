using System.Collections.Generic;
using DSX.Core.Enums;

namespace DSX.Core.Models;

public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Default Profile";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public EmulationType EmulationType { get; set; } = EmulationType.Xbox360;
    public Dictionary<ControllerButton, List<ButtonAction>> ButtonActions { get; set; } = new();
    public TriggerConfig LeftTriggerConfig { get; set; } = new();
    public TriggerConfig RightTriggerConfig { get; set; } = new();
    public TouchpadLEDConfig TouchpadLEDConfig { get; set; } = new();
    public PlayerLEDConfig PlayerLEDConfig { get; set; } = new();
    public MuteLEDConfig MuteLEDConfig { get; set; } = new();
    public RumbleConfig RumbleConfig { get; set; } = new();
    public AudioHapticsConfig AudioHapticsConfig { get; set; } = new();
    public BTHapticsConfig BTHapticsConfig { get; set; } = new();
    public StickMappingConfig LeftStickMapping { get; set; } = new();
    public StickMappingConfig RightStickMapping { get; set; } = new();
    public MotionMappingConfig MotionMapping { get; set; } = new();
    public DeadzoneConfig DeadzoneConfig { get; set; } = new();
    public TouchpadGestureConfig TouchpadGestureConfig { get; set; } = new();
}

public class ProfileSlot
{
    public int SlotIndex { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ActivateKey { get; set; } = string.Empty;
}
