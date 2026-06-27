# Session Notes — 2026-06-27

## Key Gotchas Discovered

### Unity pauses when not focused
- Unity Editor stops processing by default when another app has focus
- **Fix:** `Application.runInBackground = true;` in code (Project Settings checkbox alone didn't work in-editor)
- This caused: missed SignalR events, frozen dice animations, board not updating

### SignalR version compatibility
- SignalR Client 9.x/10.x won't work in Unity (requires .NET 10 DI abstractions)
- **Use 8.0.x** — targets .NET Standard 2.0 which Unity supports
- After NuGet changes, Unity often needs a full restart

### SignalR JSON protocol
- Server uses `System.Text.Json` with `JsonStringEnumConverter`
- Must use `JsonElement` (not `JObject` or `string`) to receive events
- Need `.AddJsonProtocol()` on the client builder to match server
- Deserialize to DTOs with `Newtonsoft.Json` (JsonConvert) after extracting raw text

### Unity Library folder
- If Unity hangs on startup after package changes, delete `Library/` folder
- It fully regenerates on next open (takes a few minutes)

### Prefab instantiation
- `Quaternion.identity` overrides prefab rotation — use `tokenPrefab.transform.rotation` to preserve it
- `Vector3.one * scale` overrides prefab shape — use `prefab.transform.localScale * scale` to preserve deformation

### Emission on URP materials
- Must call `material.EnableKeyword("_EMISSION")` for emission colour to work
- Emission adds to base colour (orange + green emission = yellow-ish) — polish later

### RenderTexture pipeline (Dice Camera → Panel)
- Camera **Target Texture** must point to the RenderTexture asset
- UI RawImage **Texture** must point to the same RenderTexture asset
- Both camera and RawImage must be enabled for it to display
- Used CanvasGroup alpha for show/hide (more reliable than SetActive)

### HTTPS required
- Server at `risk.spooch.co.uk` redirects HTTP → HTTPS
- HTTP gives 405 error on SignalR negotiate
- Always use `https://` for the production server URL

## What Was Built

| Feature | Status |
|---------|--------|
| Map + 42 territory tokens | ✅ Working |
| Live SignalR state updates | ✅ Working (with 5s poll fallback) |
| Info panel (game code, phase, players) | ✅ Working |
| Attack selection glow + pulse | ✅ Working |
| 3D dice arena with physics | ✅ Working |
| Dice panel (picture-in-picture) | ✅ Working |
| Run in background | ✅ Fixed via code |
