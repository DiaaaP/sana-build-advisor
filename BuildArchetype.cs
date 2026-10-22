namespace SanaBuildOverlay
{
    public enum BuildLifecycle
    {
        Search,
        Activation,
        Strengthening,
        Saturation,
        Blocked
    }

    public sealed class ArchetypeMatch
    {
        public string Id, Name, Evidence, NextStep, Risk;
        public BuildLifecycle Lifecycle;
        public bool Verified;
    }
}
