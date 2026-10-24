using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Rewired;
using UnityEngine;

namespace NOTGS
{
    // The toggle hotkey: flips Gaze select → Enabled from a keyboard key or a HOTAS button, and briefly
    // shows the new state on screen. HOTAS buttons belong to Rewired, which owns the joystick, so
    // Unity's Input and KeyboardShortcut never see them; they are read from Rewired by button index,
    // which Capture joystick button fills in.
    internal class GazeToggle : MonoBehaviour
    {
        private const float BannerSeconds = 1.5f;
        private float bannerUntil;
        private GUIStyle? bannerStyle;

        private void Update()
        {
            if (Plugin.CaptureJoy!.Value) { Capture(); return; }
            if (!KeyDown(Plugin.ToggleKey!.Value) && !JoyDown(Plugin.ToggleJoyButton!.Value, Plugin.ToggleJoyNumber!.Value)) return;

            Plugin.Enabled!.Value = !Plugin.Enabled.Value;
            bannerUntil = Time.unscaledTime + BannerSeconds;
            Plugin.Log?.LogInfo($"Gaze select {(Plugin.Enabled.Value ? "on" : "off")}.");
        }

        // GetKeyDown plus held modifiers rather than KeyboardShortcut.IsDown, which also requires that
        // no other key is held, so it would never fire with flight-control keys down.
        private static bool KeyDown(KeyboardShortcut sc) =>
            sc.MainKey != KeyCode.None && Input.GetKeyDown(sc.MainKey) && sc.Modifiers.All(m => Input.GetKey(m));

        // joyNum is 1-based into Rewired's joystick list; 0 matches the button on any joystick.
        private static bool JoyDown(int button, int joyNum)
        {
            if (button < 0 || !ReInput.isReady) return false;
            IList<Joystick> joys = ReInput.controllers.Joysticks;
            for (int j = 0; j < joys.Count; j++)
                if ((joyNum <= 0 || joyNum == j + 1) && button < joys[j].buttonCount && joys[j].GetButtonDown(button)) return true;
            return false;
        }

        // The first joystick button pressed after the box is ticked becomes the toggle button, pinned
        // to that joystick. GetButtonDown only sees fresh presses, so a latched switch that is already
        // on never gets captured.
        private static void Capture()
        {
            if (!ReInput.isReady) return;
            IList<Joystick> joys = ReInput.controllers.Joysticks;
            for (int j = 0; j < joys.Count; j++)
            for (int b = 0; b < joys[j].buttonCount; b++)
            {
                if (!joys[j].GetButtonDown(b)) continue;
                Plugin.ToggleJoyButton!.Value = b;
                Plugin.ToggleJoyNumber!.Value = j + 1;
                Plugin.CaptureJoy!.Value = false;
                Plugin.Log?.LogInfo($"Toggle bound to button {b} on '{joys[j].name}' (joystick {j + 1}).");
                return;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Time.unscaledTime > bannerUntil) return;
            bannerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = Screen.height / 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red },
            };
            GUI.Label(new Rect(0f, Screen.height * 0.15f, Screen.width, Screen.height / 15f),
                Plugin.Enabled!.Value ? "GAZE SELECT ON" : "GAZE SELECT OFF", bannerStyle);
        }
    }
}
