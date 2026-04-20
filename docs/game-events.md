# Game Events, Audio & Feedback

## Global Events / Delegates

```csharp
// Save / Load
SaveSystem.onSavingData       += MyMethod;   // fires when game saves
SaveSystem.onLoadingData      += MyMethod;   // fires when load starts
SaveSystem.onLoadingDataLater += MyMethod;   // fires after load completes — best for rebuilding state

// Time
TimeController.onEndOfTheDayCallback += MyMethod;  // fires at end of each in-game day

// Pause
PauseMenu.onPauseMenuOpenCallback  += MyMethod;
PauseMenu.onPauseMenuCloseCallback += MyMethod;

// World
MainGameManager.onBuyingWallEvent += MyMethod;  // fires when player buys a wall

// Input
InputManager.rebindComplete  += MyMethod;
InputManager.rebindCanceled  += MyMethod;
InputManager.rebindStarted   += MyMethod;
```

**No built-in event for device breaking** — must poll `NetworkMap` or patch `ItIsBroken()`.

## HUD Feedback

```csharp
// Message feed (bottom-left HUD ticker)
// Note the typo "Meesage" — it's in the game source, must match exactly
// Max 10 messages, each shown for 30 seconds
StaticUIElements.instance.AddMeesageInField(string message);

// Notification popup with icon
StaticUIElements.instance.SetNotification(int localisationUID, Sprite sprite = null, string text = "");

// Text input dialog (overlay — shows keyboard input box)
StaticUIElements.instance.ShowInputTextOverlay(
    string title,
    System.Action<string> onConfirmed,
    string defaultText = "",
    GameObject selectOnClose = null
);

// Error/warning 3D signs in the world (position indicator near a device)
int uid = StaticUIElements.instance.InstantiateErrorWarningSign(bool isError, Vector3 worldPos);
StaticUIElements.instance.DestroyErrorWarningSign(int uid);

// Cursor helpers
StaticUIElements.instance.ShowTextUnderCursor(string text);   // shows text below mouse pointer
StaticUIElements.instance.HideTextUnderCursor();
StaticUIElements.instance.ShowSpriteNextToPointer(Sprite s);  // icon next to cursor
StaticUIElements.instance.ClearSpriteNextToPointer();

// Key hint widget (shows "press X to..." hint)
GameObject hint = StaticUIElements.instance.CreateCustomKeyHint(
    InputAction action, int textUID, Transform parent = null, bool isPermanent = false
);
StaticUIElements.instance.RemoveCustomKeyHint();

// Hold-key progress bar (0.0–1.0)
StaticUIElements.instance.UpdateHoldProgress(float value);

// Particle effect at a transform (upgrade sparkle)
StaticUIElements.instance.InstantiateParticleUpgrade(Transform t);

// Show/hide the entire static canvas
StaticUIElements.instance.ShowStaticCanvas(bool active);
```

## Audio

```csharp
AudioManager.instance.PlayEffectAudioClip(AudioClip clip, float volume, float delayed);

// Available clips on AudioManager.instance:
// coinUse                       — coin/money sound
// AudioClipButtonHover          — button hover
// AudioClipButtonClick          — button click
// audioClipObjectiveStart       — objective started
// audioClipObjectiveEnd         — objective completed
// audioClipDeviceInserted       — device placed in rack
// audioClipOpeningBox           — opening a box
// audioClipElectronicButton     — electronic button press
// audioClipDeviceStartup        — device powering on
// audioClipSuccessfullyConnected — connection established
// audioClipRJ45[]               — cable sounds array
// audioClipImpacts[]            — impact sounds array

// Example — play coin sound when charging player
AudioManager.instance.PlayEffectAudioClip(AudioManager.instance.coinUse, 1f, 0f);
```

## Objectives

```csharp
Objectives.instance  // create / destroy / track objectives
```

## Localisation

```csharp
string text = Localisation.instance.ReturnTextByID(int uid);
// Use for displaying game text in the player's language
```

## ReusableFunctions (Static Helpers)

```csharp
ReusableFunctions.DestroyChildren(Transform root);                      // destroy all child GOs
ReusableFunctions.ChangeButtonNormalColor(Button button, Color color);  // update button color
ReusableFunctions.HexToColor(string hex);                               // parse hex color string
ReusableFunctions.NumberScrollingUI(TextMeshProUGUI text, int end);     // animated number counter
```
