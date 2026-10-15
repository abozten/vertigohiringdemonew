# Wheel Of Fortune (Vertigo Games demo)

Unity 6000.3.23f1 · uGUI + TextMeshPro · DOTween · ScriptableObjects · Sprite Atlas V2

## Rules
- Each zone you spin a wheel with 8 slices. Bronze wheel (normal zones) contains a bomb.
- Every 5th zone is a **safe zone** (silver wheel, no bomb), every 30th a **super zone** (golden wheel, special rewards).
- Rewards grow every zone (`GameSettings.RewardGrowthPerZone`).
- Bomb = all collected rewards lost. You can only leave (COLLECT in the Cash Out panel) in safe/super zones while the wheel is idle; elsewhere the button reads LOCKED and states the zone that unlocks it.

## Code layout (`Assets/_Project/Scripts`)
| Folder | What | Why |
|---|---|---|
| `Core/` (own asmdef) | `WheelSession` (game state), `IGameRules`, `GameSettings`, `WheelDefinition`, `RewardDefinition` | Rules and state, no UI. Unit tested. |
| `UI/` | `GameController` + passive views (`WheelView`, `ZoneBarView`, `RewardPanelView`, `CashOutPanelView`, `ResultPopupView`) | Controller decides, views only display. |
| `Editor/` | `ProjectSetup` | Rebuilds sprites import, atlases, data, prefabs, scene; builds APK. |

Tests: `Assets/_Project/Tests/EditMode` (Window > General > Test Runner).

`WheelSession` receives `IGameRules` through its constructor. `GameSettings` implements this contract for Inspector-configured rules; ordinary C# implementations can provide alternative zone, wheel, and reward-growth policies. The controller supplies the existing settings asset. Wheel and reward data still use ScriptableObjects.

## Editing wheel contents
`Assets/_Project/Data/Wheels/wheel_bronze|silver|golden` → change reward and base amount per slice in the Inspector.

## Build
`Tools > Wheel Of Fortune > Build Android APK` → `Builds/WheelOfFortune.apk`.

## Presentation
`Tools > Wheel Of Fortune > Refresh Presentation` regenerates the main scene and UI prefabs while preserving existing wheel contents, base amounts and game rules. It replaces the generated layout, so save custom scene edits separately before using it.

Layout: one vertical axis carries the zone strip, wheel title, wheel and hint. Two equal side columns flank it: collected rewards on the left, cash out on the right, each topped by a next safe / next super milestone badge. Slice icons and amounts orbit with the wheel but counter-rotate, so they always read upright. Headings share a drop-shadow TMP material (`Art/Fonts`). The spin button breathes while it can be pressed.

Reward sprites use cropped import rectangles with four pixels of padding; the supplied PNGs are unchanged. Slice icons choose wide, tall or square frames to keep the item above its quantity. The centered stage keeps the wheel and rewards together across the Expand canvas, with shared text sizes and a reward empty state that clears after collection and returns after loss or restart.

October 13 presentation validation: 18 static Unity captures (bronze, silver, gold, full rewards, bomb and collected popup at 16:9, 20:9 and 4:3) passed text-overflow checks. All 20 existing tests and an isolated scene lifecycle check passed. Evidence and before/after previews are in `../Review/polished-renders`; the APK has not been rebuilt for this presentation update.
