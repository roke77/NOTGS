# NOTGS: Nuclear Option Tobii Gaze Select

A [BepInEx](https://github.com/BepInEx/BepInEx) client-side mod for [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) that makes **SELECT target the unit you are looking at**, using a Tobii eye tracker, instead of the unit under the HUD's center diamond.

## What it does

Normally SELECT picks the unit marker closest to the center diamond, so you have to point your head or nose at a target before you can lock it. With NOTGS, SELECT picks the marker closest to where your eyes are on the screen.

- Everything else works the same as the normal SELECT: your TGT filters, threat priority, the select beep, the map icon, and holding SELECT to paint every unit in the area.
- If the tracker has no recent gaze point (it's off, you blinked, or you looked away), SELECT goes back to using the center diamond.
- Works alongside head tracking (Opentrack, Tobii head tracking). Head tracking moves the camera; your gaze is still a point on your screen.
- Runs entirely client-side, so it's safe on public servers.

## Requirements

- A Tobii eye tracker for PC (tested with the Eye Tracker 5) with **Tobii Experience** installed and eye tracking turned on.
- Nuclear Option running **fullscreen on the monitor your tracker is set up for**.
- For the best accuracy, run the calibration in Tobii Experience first.

VR is not supported.

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Nuclear Option, if you haven't already.
2. Download `NOTGS.dll` from the [latest release](https://github.com/roke77/NOTGS/releases/latest).
3. Copy `NOTGS.dll` into `BepInEx/plugins/` in your Nuclear Option install folder.
4. Start the game.

**Optional, recommended:** install [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to change the settings from an in-game menu (**F1**).

## How to use

Look at a unit marker and press your usual **Select** binding.

To switch gaze select on and off mid-flight, set a keyboard key or HOTAS button under **Toggle hotkey**. Pressing it shows GAZE SELECT ON or OFF on screen for a moment.

To see where you are looking, turn on **Show gaze marker**. A red circle follows your gaze while you fly, and any unit marker inside it is one SELECT can pick.

## Settings

Edit them in-game with ConfigurationManager, or in `BepInEx/config/com.roque.NOTGS.cfg`, which NOTGS creates the first time the game starts with it installed.

| Setting | Default | What it does |
|---|---|---|
| Gaze select → **Enabled** | `true` | Turn gaze select on or off. Off gives you the game's normal SELECT. |
| Gaze select → **Pick radius** | `150` | How far from your gaze, in screen pixels, a unit marker can be and still get selected (50–600). The game's own diamond uses 100. Raise it if SELECT misses units you are clearly looking at, especially near the screen edges. |
| Gaze select → **Show gaze marker** | `false` | Draw a red circle, Pick radius in size, where you are looking while flying. Handy for tuning Pick radius. |
| Toggle hotkey → **Key** | none | Keyboard key that turns gaze select on and off. |
| Toggle hotkey → **Capture joystick button** | off | Tick it, then press the HOTAS button you want as the on/off toggle. It saves the button and unticks itself. |
| Toggle hotkey → **Joystick button** / **Joystick number** | `-1` / `0` | The captured HOTAS button. You normally leave these to Capture joystick button; set **Joystick button** to `-1` to remove it. |
| Tobii → **Stream Engine path** | `C:\Program Files\Tobii\Tobii EyeX\tobii_stream_engine.dll` | Where to find Tobii's Stream Engine. Change it only if Tobii Experience is installed somewhere else. Needs a game restart. |

## Troubleshooting

Check `BepInEx/LogOutput.log` for lines from `NOTGS`:

- `Reading gaze from tobii-prp://…`: the tracker is connected.
- `Tobii Stream Engine not found`: Tobii Experience isn't installed at the configured path.
- `No Tobii eye tracker found`: the tracker isn't plugged in, or eye tracking is turned off in Tobii Experience.

## Privacy

NOTGS only uses your gaze as live input for SELECT. It never stores, logs or sends gaze data anywhere.

## License

[MIT](LICENSE). Tobii Stream Engine is Tobii's software. NOTGS does not include it; it loads the copy that Tobii Experience installs.
