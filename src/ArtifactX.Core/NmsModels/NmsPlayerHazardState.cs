using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /<h0/j3Y
/// Represents the player's hazard protection, life support,
/// environmental resistances, and suit status values.
/// </summary>
public class NmsPlayerHazardState
{
    // Hazard protection drain multiplier
    [JsonProperty("qAx")]
    public float HazardDrainMultiplier { get; set; }

    // Life support drain multiplier
    [JsonProperty("22a")]
    public float LifeSupportDrainMultiplier { get; set; }

    // Environmental resistances (array of 4 floats)
    [JsonProperty("qLk")]
    public float[] EnvironmentalResistances { get; set; }

    // Hazard protection capacity (75 = full)
    [JsonProperty("yGF")]
    public float HazardProtection { get; set; }

    // Hazard protection mode (Off, Active, etc.)
    [JsonProperty("0tA")]
    public string HazardMode { get; set; }

    // Radiation resistance
    [JsonProperty("RUO")]
    public float RadiationResistance { get; set; }

    // Life support capacity
    [JsonProperty("yuu")]
    public float LifeSupport { get; set; }

    // Toxic resistance
    [JsonProperty("n0h")]
    public float ToxicResistance { get; set; }

    // Cold resistance
    [JsonProperty(">xF")]
    public float ColdResistance { get; set; }

    // Heat resistance
    [JsonProperty("0L0")]
    public float HeatResistance { get; set; }

    // Shield strength
    [JsonProperty("HfT")]
    public float ShieldStrength { get; set; }

    // Sprint stamina multiplier
    [JsonProperty("L85")]
    public float StaminaMultiplier { get; set; }

    // Jetpack tank size (0 = default)
    [JsonProperty("HJQ")]
    public int JetpackTank { get; set; }

    // Jetpack efficiency multiplier
    [JsonProperty("FQ3")]
    public float JetpackEfficiency { get; set; }
}
