using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOTGS
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.roque.NOTGS";
        internal const string PluginName = "NOTGS";
        internal const string PluginVersion = "0.1.0";

        internal static ManualLogSource? Log;
        internal static ConfigEntry<bool>? Enabled;
        internal static ConfigEntry<float>? PickRadius;
        internal static ConfigEntry<bool>? ShowMarker;
        internal static ConfigEntry<KeyboardShortcut>? ToggleKey;
        internal static ConfigEntry<int>? ToggleJoyButton;
        internal static ConfigEntry<int>? ToggleJoyNumber;
        internal static ConfigEntry<bool>? CaptureJoy;

        private static bool spawned;

        // Everything here is static (config entries, Harmony patches, the gaze thread) or lives on the
        // NOTGS GameObject, so it keeps working after the game destroys this plugin's GameObject on the
        // way to the main menu.
        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("Gaze select", "Enabled", true,
                "SELECT picks the unit you are looking at instead of the one under the center diamond. " +
                "Without a recent gaze point (tracker off, blinking, looking away) SELECT uses the diamond as usual.");
            PickRadius = Config.Bind("Gaze select", "Pick radius", 150f,
                new ConfigDescription("How far from your gaze point, in screen pixels, a unit marker can be and still get selected. " +
                    "The game's own diamond uses 100. Raise it if gaze lands short of units near the screen edges.",
                    new AcceptableValueRange<float>(50f, 600f)));
            ShowMarker = Config.Bind("Gaze select", "Show gaze marker", false,
                "Draws a red circle, Pick radius in size, where you are looking while flying. A unit marker inside it is one SELECT can pick.");
            // KeyboardShortcut.Empty, not new KeyboardShortcut(): a static-field access guarantees the
            // type initializer that registers its config converter runs, which Mono skips for a bare
            // struct construction.
            ToggleKey = Config.Bind("Toggle hotkey", "Key", KeyboardShortcut.Empty,
                "Keyboard key that turns gaze select on and off.");
            ToggleJoyButton = Config.Bind("Toggle hotkey", "Joystick button", -1,
                "HOTAS button that turns gaze select on and off, as a Rewired button number (-1 = none). Easiest set with Capture joystick button.");
            ToggleJoyNumber = Config.Bind("Toggle hotkey", "Joystick number", 0,
                "Which joystick that button is on (1 = first, 0 = any). Capture joystick button sets it.");
            CaptureJoy = Config.Bind("Toggle hotkey", "Capture joystick button", false,
                "Tick this, then press the HOTAS button you want. It is saved and this unticks itself.");
            ConfigEntry<string> dllPath = Config.Bind("Tobii", "Stream Engine path", TobiiGaze.DefaultDllPath,
                "tobii_stream_engine.dll from your Tobii Experience install. Restart the game after changing it.");

            TobiiGaze.Start(dllPath.Value, msg => Log?.LogInfo(msg));
            SceneManager.sceneLoaded += OnSceneLoaded;

            try { new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly); }
            catch (Exception e) { Log.LogWarning($"[{PluginName}] Harmony patch failed to apply: {e.Message}"); }
        }

        // The plugin's own GameObject dies on the boot -> MainMenu transition even though BepInEx
        // marks it DontDestroyOnLoad; one created once a real scene is loaded does persist.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (spawned) return;
            spawned = true;
            var go = new GameObject("NOTGS");
            DontDestroyOnLoad(go);
            go.AddComponent<GazeMarker>();
            go.AddComponent<GazeToggle>();
        }
    }

    // TargetSelect is the game's SELECT handler: it picks among the unit markers within 100 screen
    // pixels of targetDesignator (the center diamond), then applies the TGT filters, threat priority,
    // select beep and map sync itself. Moving the diamond to the gaze point for just that call reuses
    // all of it; the diamond is back in place before the frame renders.
    [HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
    internal static class CombatHUD_TargetSelect_Patch
    {
        // Older gaze than this counts as no gaze, so SELECT falls back to the diamond.
        internal const int MaxGazeAgeMs = 200;
        private const float GameRadius = 100f;

        // Main thread only, and TargetSelect never re-enters, so plain statics hold the call's state.
        private static bool gazeActive;
        private static Vector3 savedPosition;

        private static void Prefix(CombatHUD __instance)
        {
            if (!Plugin.Enabled!.Value || !TobiiGaze.TryGetLatest(MaxGazeAgeMs, out float x, out float y)) return;
            gazeActive = true;

            Transform designator = __instance.targetDesignator.transform;
            savedPosition = designator.position;
            TobiiGaze.ToScreen(x, y, Screen.width, Screen.height, out float px, out float py);
            // z stays: InRange measures 3D distance to the markers, and only the on-screen spot should move.
            designator.position = new Vector3(px, py, savedPosition.z);
        }

        // A finalizer runs even if TargetSelect throws, so the diamond is never left at the gaze point.
        private static void Finalizer(CombatHUD __instance)
        {
            if (!gazeActive) return;
            __instance.targetDesignator.transform.position = savedPosition;
            gazeActive = false;
        }

        // Swaps the hard-coded 100 px pick radius (the constant fed straight into FastMath.InRange)
        // for PickRadius(). The paint branch's AnalyzeTarget call also passes 100f, which is why the
        // match requires the InRange call right after it.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var inRange = AccessTools.Method(typeof(FastMath), nameof(FastMath.InRange), new[] { typeof(Vector3), typeof(Vector3), typeof(float) });
            var list = new List<CodeInstruction>(instructions);
            for (int i = 0; i + 1 < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldc_R4 && list[i].operand is float f && f == GameRadius && list[i + 1].Calls(inRange))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = AccessTools.Method(typeof(CombatHUD_TargetSelect_Patch), nameof(PickRadius));
                    return list;
                }
            }
            Plugin.Log?.LogWarning($"[{Plugin.PluginName}] TargetSelect's pick radius not found (game update?). Gaze select uses the game's 100 px.");
            return list;
        }

        private static float PickRadius() => gazeActive ? Plugin.PickRadius!.Value : GameRadius;
    }
}
