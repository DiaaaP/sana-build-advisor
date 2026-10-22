using BepInEx.Configuration;
using UnityEngine;

namespace SanaBuildOverlay
{
    internal sealed class AdvisorSettings
    {
        public ConfigEntry<KeyCode> ToggleKey;
        public ConfigEntry<float> WindowX, WindowY, Scale, Opacity;
        public ConfigEntry<int> WindowMode, BuildGoal, RiskStyle;
        public ConfigEntry<bool> AutomaticCommand, ManualCommand, DetailedReasons, ExperimentalAdvice, SynergyNotifications, LockWindow, WorldMarkers, AdaptiveRisk, PerformanceMode;

        public static AdvisorSettings Bind(ConfigFile config)
        {
            var s = new AdvisorSettings();
            s.ToggleKey = config.Bind("Controls", "ToggleKey", KeyCode.F8, "Show or hide Sana Build Advisor.");
            s.WindowX = config.Bind("Window", "X", 42f, "Logical X position.");
            s.WindowY = config.Bind("Window", "Y", 65f, "Logical Y position.");
            s.Scale = config.Bind("Window", "Scale", 1f, "UI scale, 0.75 to 1.50.");
            s.Opacity = config.Bind("Window", "Opacity", 0.96f, "Panel opacity, 0.45 to 1.00.");
            s.WindowMode = config.Bind("Window", "Mode", 1, "0 icon, 1 compact, 2 expanded.");
            s.BuildGoal = config.Bind("Advisor", "BuildGoal", 0, "0 balanced, 1 damage, 2 survival, 3 mobility, 4 OP, 5 custom strategy.");
            s.RiskStyle = config.Bind("Advisor", "RiskStyle", 1, "0 safe, 1 balanced, 2 aggressive.");
            s.AutomaticCommand = config.Bind("Advisor", "AutomaticCommand", true, "Read Artifact of Command from the run.");
            s.ManualCommand = config.Bind("Advisor", "ManualCommand", false, "Manual fallback when automatic detection is off.");
            s.DetailedReasons = config.Bind("Advisor", "DetailedReasons", true, "Show detailed reasoning in expanded cards.");
            s.ExperimentalAdvice = config.Bind("Advisor", "ExperimentalAdvice", false, "Allow explicitly labelled experimental strategies.");
            s.SynergyNotifications = config.Bind("Advisor", "SynergyNotifications", true, "Show newly detected strategy notices.");
            s.LockWindow = config.Bind("Window", "LockPosition", false, "Prevent accidental window dragging.");
            s.WorldMarkers = config.Bind("Window", "WorldMarkers", true, "Show read-only markers above supported nearby loot.");
            s.AdaptiveRisk = config.Bind("Advisor", "AdaptiveRisk", false, "Temporarily select safe/balanced/aggressive scoring from live deficits.");
            s.PerformanceMode = config.Bind("Performance", "LowOverhead", true, "Update expensive scans only when their page is inspected and rebuild advice only when run state changes.");
            s.Clamp();
            return s;
        }

        public void Clamp()
        {
            Scale.Value = Mathf.Clamp(Scale.Value, 0.75f, 1.50f);
            Opacity.Value = Mathf.Clamp(Opacity.Value, 0.45f, 1f);
            BuildGoal.Value = Mathf.Clamp(BuildGoal.Value, 0, 5);
            RiskStyle.Value = Mathf.Clamp(RiskStyle.Value, 0, 2);
            WindowMode.Value = Mathf.Clamp(WindowMode.Value, 0, 2);
        }
    }
}
