# NOTGS: Nuclear Option Tobii Gaze Select

A [BepInEx](https://github.com/BepInEx/BepInEx) client-side mod for [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) that makes **SELECT target the unit you are looking at**, using a Tobii eye tracker, instead of the unit under the HUD's center diamond.

## What it does

Normally SELECT picks the unit marker closest to the center diamond, so you have to point your head or nose at a target before you can lock it. With NOTGS, SELECT picks the marker closest to where your eyes are on the screen.

- The rest of SELECT is the game's own: your TGT filters, threat priority, the select beep and the map icon all work as usual.
- Holding SELECT paints the way it normally does, around your gaze instead of the diamond: every enemy unit near where you look that your current weapon can engage.
- If the tracker has no recent gaze point (it's off, you blinked, or you looked away), SELECT goes back to using the center diamond.
- Works alongside head tracking (Opentrack, Tobii head tracking). Head tracking moves the camera; your gaze is still a point on your screen.
- Runs entirely client-side, so it's safe on public servers.

## Requirements

- A Tobii eye tracker for PC (tested with the Eye Tracker 5) with **Tobii Experience** installed and eye tracking turned on.
- Nuclear Option running **fullscreen (borderless or exclusive) on the monitor your tracker is set up for**. In windowed mode your gaze and the HUD don't line up.
- For the best accuracy, run the calibration in Tobii Experience first.

VR is not supported.

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Nuclear Option, if you haven't already.
2. Download `NOTGS.dll` from the [latest release](https://github.com/roke77/NOTGS/releases/latest).
3. Copy `NOTGS.dll` into `BepInEx/plugins/` in your Nuclear Option install folder.
4. Start the game.

**Optional, recommended:** install [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to change the settings from an in-game menu (**F1** by default).

## How to use

Look at a unit marker and press your usual **Select** binding.

**See where you are looking:** turn on **Show gaze marker**. A red circle, Pick radius in size, follows your gaze while you fly, and any unit marker inside it is one SELECT can pick. It hides whenever NOTGS has no recent gaze point.

**Switch it on and off mid-flight:** set a keyboard key or a HOTAS button under **Toggle hotkey**. Pressing it shows GAZE SELECT ON or GAZE SELECT OFF on screen for a moment, and the choice is saved for next time.

To use a HOTAS button, open the settings menu, tick **Capture joystick button**, then press the button. NOTGS saves it and unticks the box. Capture always starts unticked when the game launches. If you plug your sticks in differently and the button stops working, capture it again.

## Settings

Edit them in-game with ConfigurationManager, or in `BepInEx/config/com.roque.NOTGS.cfg`, which NOTGS creates the first time the game starts with it installed. Changes apply straight away, except **Stream Engine path**.

| Setting | Default | What it does |
|---|---|---|
| Gaze select → **Enabled** | `true` | Turn gaze select on or off. Off gives you the game's normal SELECT. |
| Gaze select → **Pick radius** | `150` | How far from your gaze, in screen pixels, a unit marker can be and still get selected (50–600). The game's own diamond uses 100. Raise it if SELECT misses units you are clearly looking at, which is most likely near the screen edges, where eye trackers are least accurate. Lower it if SELECT grabs the wrong unit in crowded areas. |
| Gaze select → **Show gaze marker** | `false` | Draw the red circle where you are looking while flying. Handy for tuning Pick radius. |
| Toggle hotkey → **Key** | none | Keyboard key that turns gaze select on and off. Works while you hold flight-control keys. |
| Toggle hotkey → **Capture joystick button** | off | Tick it, then press the HOTAS button you want as the on/off toggle. It saves the button and unticks itself. |
| Toggle hotkey → **Joystick button** / **Joystick number** | `-1` / `0` | The captured HOTAS button and the stick it is on. You normally leave these to Capture joystick button; set **Joystick button** to `-1` to remove it. |
| Tobii → **Stream Engine path** | `C:\Program Files\Tobii\Tobii EyeX\tobii_stream_engine.dll` | Where to find Tobii's Stream Engine. Change it only if Tobii Experience is installed somewhere else. Needs a game restart. |

## Troubleshooting

**SELECT still uses the center diamond:** check that **Enabled** is on (the toggle hotkey may have turned it off), that eye tracking is on in Tobii Experience, and that you are looking at the screen. Turn on **Show gaze marker**: no red circle means NOTGS has no recent gaze point.

**SELECT misses the unit you're looking at:** recalibrate in Tobii Experience, then raise **Pick radius**.

For anything else, check `BepInEx/LogOutput.log` for lines from `NOTGS`:

| Log line | Meaning |
|---|---|
| `Reading gaze from tobii-prp://…` | The tracker is connected. |
| `Tobii Stream Engine not found` | Tobii Experience isn't installed at **Stream Engine path**. |
| `No Tobii eye tracker found` | The tracker isn't plugged in, or eye tracking is turned off in Tobii Experience. Restart the game once it is on. |
| `Tobii tracker connection lost. Reconnecting.` | The tracker dropped out. NOTGS keeps retrying and logs `reconnected` when it's back. |
| `TargetSelect's pick radius not found (game update?)` | A game update changed SELECT. Gaze select still works, but **Pick radius** is stuck at the game's 100 px until NOTGS is updated. |

## Privacy

NOTGS only uses your gaze as live input for SELECT. It never stores, logs or sends gaze data anywhere.

## Uninstall

Delete `NOTGS.dll` from `BepInEx/plugins/`, and `com.roque.NOTGS.cfg` from `BepInEx/config/` if you want the settings gone too.

## Build

Requires a local Nuclear Option install (for `Assembly-CSharp.dll`). Create a `GameDir.props` next to `NOTGS.csproj` pointing at your game folder (see the comment at the top of `NOTGS.csproj`), then:

```bash
dotnet build -c Release
```

The build copies `NOTGS.dll` into the game's `BepInEx/plugins/` folder.

`tools/GazeCheck` checks the gaze reader against a real tracker outside the game. With eye tracking on and you looking at the screen, it passes once it reads a fresh gaze point:

```bash
dotnet run -c Release --project tools/GazeCheck
```

## License

[MIT](LICENSE) © Roque Alejandro Cuello. Tobii Stream Engine is Tobii's software. NOTGS does not include it; it loads the copy that Tobii Experience installs.
