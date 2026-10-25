using UnityEngine;

namespace NOTGS
{
    // Optional red ring at the gaze point, sized to Pick radius: a unit marker inside it is one
    // SELECT can pick. Drawn only in flight, where SELECT applies.
    internal class GazeMarker : MonoBehaviour
    {
        private const float Thickness = 3f;   // ring line width in screen pixels
        private Texture2D? ring;
        private int ringRadius;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Plugin.ShowMarker!.Value) return;
            CombatHUD hud = SceneSingleton<CombatHUD>.i;
            if (hud == null || hud.aircraft == null) return;
            if (!TobiiGaze.TryGetLatest(CombatHUD_TargetSelect_Patch.MaxGazeAgeMs, out float x, out float y)) return;

            int radius = Mathf.RoundToInt(Plugin.PickRadius!.Value);
            if (ring == null || radius != ringRadius) { Destroy(ring); ring = MakeRing(radius); ringRadius = radius; }

            // Same mapping the SELECT patch uses; IMGUI's y runs top-down, Unity screen y bottom-up.
            TobiiGaze.ToScreen(x, y, Screen.width, Screen.height, Plugin.OffsetX!.Value, Plugin.OffsetY!.Value, out float px, out float py);
            float size = ring.width;
            GUI.DrawTexture(new Rect(px - size / 2f, Screen.height - py - size / 2f, size, size), ring);
        }

        // Built at the exact on-screen size so the line stays Thickness px wide at any radius.
        // Rebuilt only when Pick radius changes.
        private static Texture2D MakeRing(int radius)
        {
            int size = radius * 2 + 4;
            float c = (size - 1) / 2f;
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float dx = px - c, dy = py - c;
                float edge = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius);
                float alpha = Mathf.Clamp01(Thickness / 2f + 0.5f - edge);   // 1 px anti-aliased edge
                pixels[py * size + px] = new Color32(255, 0, 0, (byte)(alpha * 220));
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
