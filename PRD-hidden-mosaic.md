# PRD – Hidden Mosaic: Meta Game, Economy & Juicy UI

Version 1.0 · 14 October 2026 · Companion to `TRD-2d-mosaic-hog.md` (core hunt) and `TRD-hidden-mosaic-meta.md` (this feature's tech design).

## 1. Problem
The magnifier hunt is fun but has no wrapper around it. There is no reason to come back, no reward for playing well, no visual personality, and nowhere to sell hints or remove ads. Menus are dark placeholder panels, and losing a level drops the player onto a Retry button with no second chance.

## 2. Goals
| Goal | Measure |
|---|---|
| A vibrant, juicy look that reads as a polished casual game | Every tap, find, win and purchase has motion + sound + haptic feedback |
| A long, readable progression from easy to hard | A level map grouped into chapters, with stars per level |
| An economy with real sinks and sources | Coins + Hints, rewarded-ad hooks, and IAP rows ready for store setup |
| Content scales without code | New silhouettes and image HOGs are catalog rows plus a sort/validate menu |
| A slot for the real-image HOG (built separately) | One image-HOG node after every 5 mosaic levels |

Non-goals (future): daily rewards, lives/energy, tutorial/FUE, live events, real store SKUs and pricing.

## 3. Player loop
```
Home ──PLAY──▶ Level Intro card ──▶ Hunt ──win──▶ Win popup (★, coins, ×2 ad) ──Next──▶ next node
  │                                  │
  │                                  ├─time up──▶ Fail popup (+30s coins / +30s ad / Retry / Home)
  │                                  └─hint, 0 left──▶ Jam popup (ad / coins / Shop)
  └─Map / Shop / Settings
Chapter = 5 mosaic levels → 1 Image-HOG level → Chapter Complete (chest)
```

## 4. Screens
### 4.1 Home
- Violet→pink gradient background with slowly drifting, rotating mosaic tiles.
- "Hidden Mosaic" logo, gently bobbing.
- Large yellow PLAY button labelled "Level N" with an idle pulse. It continues at the first uncleared node.
- Top bar with a **Coins pill** and a **Hints pill**. Each has a "+" that opens the Shop. Pills punch and the counter rolls when the balance changes.
- Bottom nav with three round buttons: **Map · Shop · Settings**.
- A star total badge ("★ 23").

### 4.2 Level map (progression)
- Vertical scroll. A chapter header ("Chapter 2 · Night Market") sits above each group of 6 nodes.
- Nodes zig-zag left and right along a dotted path.
- **Mosaic node:** round badge with the level number, toughness colour ring (1 green, 2 yellow, 3 orange, 4 red, 5 purple), and 0–3 stars underneath.
- **Image-HOG node:** larger gold badge with a picture icon, labelled "Photo Hunt".
- **States:**
  - *Cleared:* shows its stars.
  - *Current:* pulses, with a bobbing arrow.
  - *Locked:* grey with a padlock. Tapping it shows the toast "Finish level N first". The All-Levels bundle unlocks everything.
- On open, the map auto-scrolls to the current node. Tapping a node opens the Level Intro popup.

### 4.3 Shop (popup, available everywhere including mid-level)
| Section | Items |
|---|---|
| Free | "+40 coins – watch video" (5-minute cooldown, at most 5 per session) |
| Hints for coins | 1 hint – 50 coins · 5 hints – 200 coins |
| Coin packs (IAP) | 500 coins · 3000 coins |
| Hint packs (IAP) | 10 hints · 30 hints |
| Premium | Remove Ads · Theme Pack · All Levels |
| — | Restore Purchases |

IAP buttons show the live store price when it is available, otherwise "Buy".

### 4.4 Settings (popup)
- Toggles: Music, Sound FX, Vibration. All persist.
- Restore Purchases.
- Privacy Policy link.
- Version label.
- Dev builds only: "Reset progress" and "+1000 coins".

### 4.5 Gameplay HUD
- **Top-left:** Pause button.
- **Top-right:** seven-segment timer. It pulses red under the urgent threshold.
- **Below the timer:** Hint button with a count badge. The badge reads "AD" when hints are 0.
- **Bottom tray:** target icons. Each pops in, and flies out when found.
- **Combo toast:** "Nice!", "Great!" or "Amazing!" when finds are less than 2.5s apart.
- **Wrong pick:** red X, camera shake and haptic.

### 4.6 Popups
| Popup | Contents |
|---|---|
| **Level Intro / Level Jump** | "Level 7", toughness name and colour (Easy / Medium / Tricky / Hard / Expert), targets count, time limit. A "DIFFICULTY UP!" ribbon appears when toughness is higher than the previous level's. The **Play** button starts the timer. |
| **Win** | "Great Job!" banner. Stars drop in one by one with rising pitch. A coin counter rolls up, then coins fly to the pill. Confetti. Buttons: **×2 Coins (video)**, **Next**, **Home**. |
| **Fail (Time's up)** | "Out of time!", found X / Y. Buttons: **+30s · 60 coins** (cost grows by 30 per continue), **+30s free (video, once per level)**, **Retry**, **Home**. |
| **Jam (Out of hints)** | **Watch video** (+1 hint), **Use 50 coins**, **Go to Shop**. |
| **Jam (Not enough coins)** | **Watch video** (+40 coins), **Go to Shop**. |
| **Pause** | Resume, Restart, Home, plus the Music/SFX/Vibration toggles. |
| **Chapter Complete** | Chest bounce and open, "+150 coins", "Chapter N complete!", **Continue**. |
| **Photo Hunt – Coming soon** | Placeholder shown until the image-HOG scene ships. **Continue** marks the node done. |

## 5. Economy
All values are remote-config tunable (`eco_*` keys).

| Lever | Default |
|---|---|
| Starting grant (once) | 200 coins, 3 hints |
| Win coins | `(20 + 10 × toughness) × starMult` with 1★ ×1, 2★ ×1.5, 3★ ×2 |
| Replay of a cleared level | 25% of win coins |
| Image-HOG chapter chest | 150 coins |
| 3★ | ≥ 50% time left **and** no hints used |
| 2★ | ≥ 25% time left |
| 1★ | completed |
| Hint (coins) | 50 |
| Continue +30s | 60 coins, +30 per extra continue in the same level |
| Rewarded: hint | +1 hint, used immediately |
| Rewarded: continue | +30s, once per level |
| Rewarded: double win | ×2 coins for that win |
| Rewarded: free coins | +40, 5-minute cooldown, at most 5 per session |
| IAP | coins_small 500 · coins_large 3000 · hints_small 10 · hints_large 30 · remove_ads · theme_pack · bundle_all_levels |

Interstitials keep their current cadence (`mon_interstitial_level_interval`, default every 3 level transitions), and are suppressed for remove-ads owners.

## 6. Content & difficulty
- The play order is the order of `catalog.json` `levels[]`.
- Each row's `toughness` (1–5) drives the colour, the intro ribbon and the coin reward.
- **Adding silhouettes:**
  1. Drop `name.json` + `name.png` into `Resources/HiddenMosaic/SilhouetteJson/`.
  2. Add a row to `catalog.json`.
  3. Run **Nixin Studio ▸ Hidden Mosaic ▸ Sort Catalog by Toughness**. This is a stable sort, so hand order within a band is kept.
  4. Run **Validate Catalog**.
- **Image HOGs** are rows in `catalog.json` → `image_hogs[]`: `{ "id", "title", "scene", "time_limit_seconds", "toughness" }`.
  - They are inserted after every 5 mosaics (`hunt_mosaics_per_chapter`).
  - Missing rows become "Coming soon" placeholder nodes.

## 7. Analytics
New events:
- `currency_earn {currency, amount, source}`
- `currency_spend {currency, amount, sink}`
- `popup_view {popup}`
- `shop_view {from}`
- `continue_used {method}`
- `stars_awarded {level_id, stars}`
- `image_hog_start {id}`
- `image_hog_complete {id, success}`

## 8. Acceptance
- A fresh install shows 200 coins and 3 hints on Home.
- The map shows 15 mosaic nodes and 3 Photo Hunt nodes (after #5, #10 and #15).
- A win awards stars and coins and unlocks the next node. Replaying keeps the best stars.
- Time-out followed by a coin continue resumes the clock with +30s and charges coins.
- Using the last hint, then tapping Hint again, shows the Jam popup. Its Shop button opens the Shop over the level, and closing the Shop returns to the level with the timer still paused until then.
- Android back closes the top popup first, then pauses or leaves.
- Settings toggles persist across restarts.
