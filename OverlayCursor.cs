using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SanaBuildOverlay
{
    public sealed partial class BuildOverlay
    {
        private MPEventSystem cursorOwner;
        private bool cursorAcquired;
        private bool overlayInputActive;
        private CursorLockMode savedLock;
        private bool savedVisible;

        private void LateUpdate() { UpdateCursor(); }
        private void OnDisable() { ReleaseCursor(); }

        private void UpdateCursor()
        {
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool needed = InteractionPolicy.WantsOverlayInput(visible, altHeld);
            overlayInputActive = needed;
            var user = LocalUserManager.GetFirstLocalUser();
            var owner = user == null ? null : user.eventSystem;
            if (!needed || (cursorAcquired && owner != cursorOwner)) ReleaseCursor();
            if (!needed) return;
            if (!cursorAcquired)
            {
                savedLock = Cursor.lockState;
                savedVisible = Cursor.visible;
                cursorOwner = owner;
                if (cursorOwner != null) cursorOwner.cursorOpenerCount++;
                cursorAcquired = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseCursor()
        {
            overlayInputActive = false;
            if (!cursorAcquired) return;
            if (cursorOwner != null) cursorOwner.cursorOpenerCount = Mathf.Max(0, cursorOwner.cursorOpenerCount - 1);
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
            cursorOwner = null;
            cursorAcquired = false;
        }
    }
}
