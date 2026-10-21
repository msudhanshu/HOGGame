# TRD – Hidden Mosaic: Meta Game, Economy & Juicy UI

Version 1.0 · 14 October 2026 · Implements `PRD-hidden-mosaic.md`.

## 1. Principles
- **Core vs Unity.** Rules (stars, coins, roadmap, progress, easing) live in `Game.Core`. That assembly has no engine references and is unit-tested. Views, juice and services live in `Game.Unity`.
- **Shared packages first.**
  - `com.nixin.ui`'s `UiNavigator` provides the popup stack, dimmer and Android back.
  - `com.nixin.monetization`'s `Monetization.Request` provides ads and IAP, and `GameServices.Wallet` provides balances.
  - `NixinIcons` and `NixinAudio` provide glyphs and clips.
  - Game code never calls a vendor SDK directly.
- **Code-built, themeable UI.** Every colour, font and optional sprite comes from `HuntUiTheme` (`Resources/HuntUiTheme`, optional; built-in defaults otherwise). Rounded and gradient sprites are generated at runtime by `HuntUiSprites`. Real art can be dropped into the theme later without code changes.
- **Data-driven content.** `catalog.json` (levels + image_hogs) plus remote config `eco_*` keys.

## 2. Module map
```
Assets/Game/Core/
  HuntCatalog.cs            + ImageHogs list
  HuntLaunchRequest.cs      + node index / image-hog launch
  HuntLevelClock.cs         + AddTime
  Economy/HuntEconomyRules.cs     HuntEconomyTuning, StarsFor, CoinsForWin, ContinueCost
  Progression/ImageHogDef.cs
  Progression/HuntRoadmap.cs      HuntNode, Build(catalog, imageHogs, perChapter)
  Progression/HuntProgressState.cs  best stars per node id, highest unlocked node
  Juice/Ease.cs
Assets/Game/Unity/
  Ui/Theme/  HuntUiTheme (SO) · HuntUiSprites · HuntUiKit
  Ui/Juice/  HuntTweener · HuntButtonJuice · HuntIdlePulse · HuntCounterText · HuntCoinFly · HuntConfetti · HuntShake · HuntComboToast
  Ui/Popups/ HuntPopupBase<T> · Win · Fail · Jam · LevelIntro · Chapter · Pause · Settings · Shop · ComingSoon
  Ui/        HuntPopups (typed openers) · HuntCurrencyBar · HuntMapScreen · HuntMapNode · HuntToast
  Services/  HuntSettings · HuntAudio · HuntHaptics
  Economy/   HuntWallet · HuntOfferPresenter
  Progression/ HuntSave · HuntImageHogBridge
  HuntHomeScreen (rewritten) · PlayHost (flow) · HuntHud (skin, pause, hint badge)
  Editor/HuntCatalogTools  Sort by Toughness · Validate
Assets/Tests/EditMode/ Game.Core.Tests
```

## 3. Core contracts
```csharp
public sealed class HuntEconomyTuning {           // defaults; Unity overlays remote config
  int StartCoins=200, StartHints=3, WinBase=20, WinPerToughness=10;
  float Star2Mult=1.5f, Star3Mult=2f, ReplayFactor=.25f;
  float Star3TimeFrac=.5f, Star2TimeFrac=.25f;
  int ChapterChest=150, HintCoinCost=50, ContinueBase=60, ContinueStep=30;
  float ContinueSeconds=30f; }
static int  HuntEconomyRules.StarsFor(float timeLeftFrac, int hintsUsed, HuntEconomyTuning t);
static int  HuntEconomyRules.CoinsForWin(int toughness, int stars, bool firstClear, HuntEconomyTuning t);
static int  HuntEconomyRules.ContinueCost(int continuesUsed, HuntEconomyTuning t);

enum HuntNodeKind { Mosaic, ImageHog }
sealed class HuntNode { Kind; Index; CatalogIndex; ImageHog; Chapter; Number; Id; IsPlaceholder; }
static IReadOnlyList<HuntNode> HuntRoadmap.Build(HuntCatalog c, int perChapter);   // uses c.ImageHogs

sealed class HuntProgressState { int HighestUnlocked; int StarsFor(id); bool IsCleared(id);
  bool RecordWin(int nodeIndex, string id, int stars); int TotalStars; ToJson/FromJson (string-only format) }
```
- **Node id.** Mosaic: `m:<file>#<occurrence>`, where occurrence counts duplicate files in the catalog. Image HOG: `h:<id>`. A placeholder: `h:chapter<N>`. Stars survive catalog reordering because they are keyed by id. Unlock is by node index, so re-sorting the catalog never re-locks a level the player has already reached.
- **Roadmap.** For each chapter `c`, take `perChapter` mosaics, then image HOG `c` (or a placeholder) — but only if the chapter is full. A trailing partial chapter has no HOG.

## 4. Unity systems
### 4.1 UI stack
- **Popups.** `HuntPopupBase<T> : UiView<T>` builds its own card in code on first use (`EnsureBuilt`).
  - `HuntPopups.Open<TView,TPayload>()` registers the template lazily with `UiNavigator.Ensure()`, following the same pattern as `MonetizationOfferPresenter`.
  - The navigator's canvas is `DontDestroyOnLoad`, sort order 100, so it sits above the HUD and Home canvases.
  - Each scene calls `HuntPopups.CloseAll()` on load.
- **Animation.**
  - `OnOpened` runs a dimmer fade, then the card scales 0.6→1 with OutBack (0.35s), then children stagger in.
  - `Dismiss(Action after)` scales the card out (0.15s), then calls `UiNavigator.Current.Close()`, then `after`.
- **Back key.** Every popup sets `SetBackHandler` to its natural "close" action. Fail and Win use Home. Pause and Shop use Resume/Close.
- **Screens.** The Home scene keeps its own canvas (`HuntHomeScreen`). Its home and map panels are children, switched with a slide and fade. The `UiNavigator.UnhandledBack` hook decides map → home → minimize.

### 4.2 Juice
- **`HuntTweener`.** A single hidden runner. `HuntTweener.To(Object owner, float dur, Action<float> step, Func<float,float> ease, float delay, Action done)` returns a handle. `Kill(owner)` stops a tween. Tweens run on unscaled time. Helpers: `Scale`, `Fade` (CanvasGroup), `Move`, `Punch`, `Color`.
- **`HuntButtonJuice`** (an `IPointerDown`/`IPointerUp` handler): squashes to 0.9 and recovers with OutBack. It plays Click SFX and fires `HuntHaptics.Light`. It is added by every kit button.
- **`HuntCoinFly.Play(from, toPill, count, onEach, onDone)`** pools up to 12 coin images. Each follows a quadratic Bézier with a random control point and a staggered start. Each arrival calls `onEach`, which punches the pill and ticks the counter.
- **`HuntConfetti.Burst(parent, origin, n=60)`** pools rects. Each gets a random theme colour, velocity, gravity and spin, with a 1.6s lifetime.
- **`HuntShake.Camera(cam, amp, dur)`** applies a damped noise offset and restores the camera exactly.
- **Find FX.** Existing `HuntFindFx` / `HuntWrongFx` are extended with `HuntComboToast`, haptics and shake.

### 4.3 Services
- **`HuntSettings`.** `Music`, `Sfx` and `Haptics` bools in PlayerPrefs (`hunt.settings.*`), with a `Changed` event.
- **`HuntAudio`.** A `DontDestroyOnLoad` object with two `AudioSource`s (music loop, SFX).
  - `Sfx(path, pitch=1)` resolves through `NixinAudio.Load`.
  - `MusicClip` comes from the theme, or nothing is played.
  - Both respect `HuntSettings`.
  - `PlayHost.PlayClip` routes through it.
- **`HuntHaptics`.** `Light`/`Medium`/`Heavy`.
  - Android: `android.os.Vibrator.vibrate(VibrationEffect.createOneShot(ms, amp))` on API 26+, `vibrate(ms)` otherwise.
  - iOS / other: `Handheld.Vibrate()` for Heavy only.
  - Gated by `HuntSettings.Haptics`.

### 4.4 Economy
- **`HuntWallet`** (static):
  - Balances: `Coins`, `Hints`.
  - Changes: `EnsureStarterGrant()` (flag `hunt.eco.starter`), `Earn(currency, amount, source)`, `TrySpend(currency, amount, sink)`.
  - `Tuning` reads `eco_*` from remote config over the `HuntEconomyTuning` defaults.
  - It always goes through `GameServices.Wallet` and logs `currency_earn` / `currency_spend`.
- **Game-owned jams.** The game owns its jam flows, so it can offer a coins option:
  - Hint: `HuntWallet.TrySpend(Hints, 1)` → reveal. Otherwise the Jam popup offers:
    - Ad: `Monetization.Request("hint_ad")`, which rewards +1 hint and spends 1.
    - Coins: `TrySpend(Coins, 50)`.
    - Shop.
  - Continue: `TrySpend(Coins, ContinueCost(n))` → +30s. Otherwise the coins Jam popup offers `free_coins` ad or Shop.
- **`HuntOfferPresenter`** replaces the default offer popup for any remaining `SpendOrOffer` placement with the themed Jam popup (ad + product buttons + Shop link). It uses `replacePopups:false` so the popup underneath survives.

### 4.5 Monetization catalog rows
| id | kind | details |
|---|---|---|
| hint | SpendOrOffer | hints ×1, ad reward 2 hints, fallback `hints_small` (kept for back-compat) |
| hint_ad | RewardedAd | reward hints 1, cost hints 1 |
| continue_ad | RewardedAd | — (once per level, enforced in game) |
| double_coins | RewardedAd | — |
| free_coins | RewardedAd | reward coins 40, cooldown 300s, session cap 5 |
| interstitial_level_complete | Interstitial | unchanged |
| unlock_level | RewardedAd | unchanged (unused by the new map) |
| remove_ads_purchase / theme_pack_purchase | Purchase | unchanged |
| buy_coins_small / buy_coins_large / buy_hints_small / buy_hints_large / buy_all_levels | Purchase | → products |

New products: `coins_small` (coins 500), `coins_large` (coins 3000), `hints_large` (hints 30). Existing products are kept.

### 4.6 Progression & save
- **`HuntSave`** (static) keeps a `HuntProgressState` as JSON in PlayerPrefs `hunt.save.v1`.
  - **Migration:** on first load it reads legacy `hunt.progress.highest_unlocked` (a mosaic index) and converts it to a node index through the roadmap.
  - `IsUnlocked(node)` is true when `node.Index <= HighestUnlocked` or the `all_levels` entitlement is owned.
  - **Pending result:** `PendingResult` carries a win reported from another scene (an image HOG) to Home.
- **`HuntRoadmapService`** caches `HuntRoadmap.Build(HuntRemoteConfig.Apply(HuntCatalogLoader.Load(asset)), perChapter)`.
- **`HuntImageHogBridge`** — the contract for the other session:
  ```csharp
  // launch (called by map / Next):
  HuntImageHogBridge.Launch(HuntNode node);
  //   real scene  -> HuntLaunchRequest.SetImageHog(node.Index, node.ImageHog); SceneManager.LoadScene(def.Scene)
  //   placeholder -> HuntComingSoonPopup → ReportResult(true, 0f, 0) + Chapter popup
  // inside the image-HOG scene:
  var def = HuntLaunchRequest.ImageHog;           // id, title, time limit, toughness
  HuntImageHogBridge.ReportResult(bool success, float timeLeftFrac, int hintsUsed);
  HuntImageHogBridge.ReturnToHome();              // Home applies stars/coins + Chapter popup
  ```
  A scene counts as available when `def.Scene` is non-empty and `Application.CanStreamedLevelBeLoaded(def.Scene)` returns true.

### 4.7 Play flow (`PlayHost`)
1. Resolve the node from `HuntLaunchRequest.NodeIndex`. A non-mosaic node bounces back to the bridge.
2. `LoadLevel` builds the board. The clock is reset and **paused**, and the Level Intro popup opens. Its Play button starts the clock.
3. The clock ticks and picks are accepted only when `!Paused`. `Paused` is any of: a `UiNavigator` popup is open, an ad is playing, or the Intro is open.
4. **Win:**
   - `stars = StarsFor(clock.Remaining / limit, hintsUsed)`
   - `firstClear = HuntSave.RecordWin`
   - `coins = CoinsForWin`
   - `HuntWallet.Earn`
   - Win popup (×2 → `double_coins` grants the same amount again).
   - **Next:** `MaybeShowInterstitial`, then go to the next node. A mosaic node loads in place; an image HOG goes through the bridge.
5. **Time-out:** Fail popup. A continue calls `clock.AddTime(ContinueSeconds)`. Retry reloads the level and Home exits.
6. **Pause button / Android back:** Pause popup.

### 4.8 Content tooling
`Editor/HuntCatalogTools`:
- **Sort Catalog by Toughness.** A stable sort of `levels[]` by `toughness`, rewritten with 2-space JSON. `image_hogs[]` is kept as is.
- **Validate Catalog.** Checks that each file exists, the preview png exists, toughness is within 1..5, and ids are unique. It logs the chapter layout.

## 5. Remote config keys (new)
- `eco_start_coins`, `eco_start_hints`
- `eco_win_base`, `eco_win_per_toughness`
- `eco_replay_factor`, `eco_chapter_chest`
- `eco_hint_coin_cost`
- `eco_continue_base`, `eco_continue_step`, `eco_continue_seconds`
- `hunt_mosaics_per_chapter`

## 6. Testing
- **EditMode (`Assets/Tests/EditMode/Game.Core.Tests`):**
  - `HuntEconomyRulesTests`
  - `HuntRoadmapTests`
  - `HuntProgressStateTests`
  - `HuntLevelClockTests`
- **Manual / Play mode:** the acceptance list in the PRD §8. In Editor, monetization runs on the Simulated ads/store, so ads and purchases succeed instantly.

## 7. Risks
- **Code-built UI cost.** The UI is rebuilt every scene load (about 1–2 ms). Popup templates persist in the `DontDestroyOnLoad` navigator.
- **Legacy `Text`.** It is used because the project has no TMP essentials imported. Swapping in a theme `Font` gives brand typography.
- **Scene-authored Home children.** Home.unity's pre-authored children (`HomePanel`, `LevelSelect`, `Shop`) are purged at runtime when the `HomeV2` marker is missing.
