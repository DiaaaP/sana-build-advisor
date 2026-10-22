namespace SanaBuildOverlay
{
    // Independent of Unity so this input policy is covered by unit tests.
    public static class InteractionPolicy
    {
        public static bool WantsOverlayInput(bool overlayVisible, bool altHeld)
        {
            return overlayVisible && altHeld;
        }
    }
}
