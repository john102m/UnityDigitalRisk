# How the Dice Arena Works

A complete breakdown of everything needed to show 3D dice rolling in a panel on screen.

## The Problem

When a player rolls dice on their phone, the Unity board needs to show a physical dice simulation in real time. The dice are purely visual — the server already decided the results.

## The Solution — Layer by Layer

### 1. The Physical Box (Scene Objects)

Five cubes forming an open-top box, positioned far off-screen at `(50, 0, 0)` so it never interferes with the map:

```
DiceArena (Empty GameObject at position 50,0,0)
├── Floor      (Cube: scale 5×0.1×5 — flat surface)
├── WallLeft   (Cube: scale 0.1×2×5 — at x=-2.5)
├── WallRight  (Cube: scale 0.1×2×5 — at x=+2.5)
├── WallFront  (Cube: scale 5×2×0.1 — at z=-2.5)
├── WallBack   (Cube: scale 5×2×0.1 — at z=+2.5)
├── SpawnPoint (Empty at y=3 — where dice appear above the box)
└── DiceCamera (Camera looking down into the box)
```

The walls have colliders (default on cubes) so dice bounce off them. No Rigidbody on the box — it's static scenery.

### 2. The Dice Prefab

A simple cube stored in `Assets/Prefabs/`:
- **Scale:** 0.6×0.6×0.6 (smaller than the box walls)
- **Rigidbody:** Makes it respond to gravity and physics
- **BoxCollider:** Default on a cube — detects collision with walls/floor
- **PhysicsMaterial:** "DiceBounce" — bounciness 0.3, friction 0.4 (settled, not too crazy)

At runtime, the script spawns copies of this prefab, applies random spin, and gravity does the rest.

### 3. The Camera → RenderTexture → RawImage Pipeline

This is the clever bit. Instead of switching the whole screen to the dice view:

```
DiceCamera ──renders to──▶ RenderTexture (DiceRT) ──displayed by──▶ RawImage (DicePanel)
```

- **DiceCamera:** A second camera pointing into the box. It doesn't render to screen — it renders to a texture.
- **RenderTexture (DiceRT):** An asset created in the Project panel. Think of it as a virtual screen that the camera draws to.
- **RawImage (DicePanel):** A UI element on the Canvas that displays the RenderTexture. Position and size it anywhere on screen.

This means the map stays visible behind the dice panel — picture-in-picture.

### 4. The Scripts

#### SignalRClient.cs
Already listening for `CombatResult` from the server. When a player rolls dice on their phone, the server broadcasts the result (dice values, who lost what).

#### CombatTheatre.cs (orchestrator)
- Receives `CombatResult` events
- Queues them (in case multiple arrive during animation)
- Shows the dice panel (enables camera + sets alpha to 1)
- Tells DiceRoller to roll
- Waits for roll to complete
- Hides the dice panel

#### DiceRoller.cs (physics)
- Spawns dice prefabs above the box (attacker left side, defender right side)
- Applies material (red for attacker, white for defender)
- Gives each die random rotation + downward velocity + spin
- Physics engine takes over — dice fall, bounce off walls, tumble, settle
- Detects when all dice stop moving (velocity below threshold)
- Corrects final orientation so top face shows the server's value
- Waits 1.5 seconds so player can read the result
- Signals completion

#### DiceFaceReader.cs (face detection)
- Attached to each die at spawn time
- Checks which of the 6 face normals (up/down/left/right/forward/back) is most aligned with world-up
- Returns the number on that face (standard die: opposite faces sum to 7)

### 5. The Timing Sequence

```
Player taps "Roll" on phone
    │
    ▼
Server resolves combat, broadcasts CombatResult {attackerDice: [6,4,2], defenderDice: [5,3]}
    │
    ▼
SignalRClient receives event, fires OnCombatResult
    │
    ▼
CombatTheatre.OnCombatResult:
    ├── Deserializes JSON
    ├── Queues result
    └── Starts ProcessQueue coroutine
        │
        ▼
    PlayCombatSequence:
        ├── Shows dice panel (camera on, alpha 1)
        ├── Calls DiceRoller.RollDice(attackerValues, defenderValues)
        │       ├── Clears old dice
        │       ├── Spawns 3 red + 2 white dice above box
        │       ├── Applies random force + torque
        │       ├── Waits for physics settle (~2 seconds)
        │       ├── Corrects faces to match server values
        │       └── Waits 1.5s for display
        ├── Hides dice panel (camera off, alpha 0)
        └── Destroys dice objects
```

### 6. The "Cheat"

The dice don't determine the result — they perform it. The server already rolled `[6, 4, 2]`. Unity's physics will land on random faces, so after settling the script snaps each die to the correct orientation. This happens after the tumbling stops, so it looks natural.

It's the same trick every online casino uses. The animation is entertainment; the numbers are predetermined.

### 7. What You Created in the Editor

| Thing | Type | Purpose |
|-------|------|---------|
| DiceArena | Empty GameObject | Container, positioned far from map |
| Floor + 4 Walls | Cubes | Physical box for dice to bounce in |
| SpawnPoint | Empty GameObject | Marks where dice appear |
| DiceCamera | Camera | Films the box from above |
| DiceRT | RenderTexture asset | Virtual screen camera draws to |
| DicePanel | UI RawImage | Displays the RenderTexture on screen |
| Die | Prefab (Cube + Rigidbody) | Template for spawning dice |
| Red/White materials | Materials | Attacker/defender colours |
| DiceBounce | PhysicsMaterial | Controls bounciness/friction |

### 8. What the Code Created

| Thing | Script | Purpose |
|-------|--------|---------|
| Event plumbing | SignalRClient | Routes server events to Unity |
| Sequence logic | CombatTheatre | Show panel → roll → hide panel |
| Physics spawn | DiceRoller | Creates dice, applies force, waits |
| Face reading | DiceFaceReader | Determines which number is face-up |

---

*Total: 5 scene objects (box), 1 prefab, 1 render texture, 1 UI element, 2 materials, 1 physics material, 4 scripts.*
