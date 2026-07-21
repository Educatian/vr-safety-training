# User-Testing Feedback Resolution Record

- Date: 2026-07-21
- Feedback source: Stephen Abu + David Awoyemi joint pilot test (email, full text on file)
- Resolution commits: earlier fixes + `20a93e8` (feat: resolve user-testing feedback)
- Verification: full EditMode suite re-run via Unity MCP on 2026-07-21 21:46-21:48Z —
  **205/205 passed, 0 failed, 0 skipped** (108 s; `AppData/LocalLow/DefaultCompany/vr-safety-training/TestResults.xml`)

## Item-by-item mapping

| # | Feedback (paraphrased) | Resolution | Evidence |
|---|------------------------|------------|----------|
| 1 | No way to end/exit/minimize the simulation; had to Alt+Tab | ESC pause menu (RESUME/QUIT); locomotion and world pointer blocked while open; chat keeps ESC priority via frame stamp | `Scripts/Runtime/PauseMenuController.cs`, screenshot `docs/images/pause-menu` |
| 2 | Construction tutor avatar slides instead of walking | Walk animation fix (earlier commit; confirmed resolved in the 7/21 cross-check) | Session cross-check, editor observation |
| 3 | Objects well rendered, good visual quality | Positive comment — no action | — |
| 4 | Walls not fully restrictive; avatar passes through | Wall collider fix + full perimeter reseal: corrugated hoarding on all four sides of all six sites, 12 gate panels sealing the 1.25 m doorway gaps | 360°×5° ray probe: 0 escapes at all 6 sites |
| 5 | Tutor responses appear in batches (1/14, 2/14, ...) | Batching removed; full response rendered at once | Session cross-check |
| 6 | Font size/style unfriendly; labels conflict and overwhelm (e.g., cement area) | Inter Regular/SemiBold (SIL OFL) embedded; fixed 18 pt (best-fit shrink removed); proximity labels thin out dense areas | `Editor/SafetyUiFonts.cs`, `Assets/SafetyTraining/Fonts/`, screenshot `inter-font-board` |
| 7 | Cliff-like open edges distract/scare users in HMD | Root cause: world expansion left visual hoarding at the old ±4.8 m line while the ±15 m boundary was invisible colliders only — visual hoarding rebuilt at the true boundary | Commit `20a93e8`, screenshot `entry-gates-sealed` |
| 8 | Object labels should appear only on approach | `ProximityLabel` on 194 prop labels (8 m radius, staggered 0.3 s checks); large wayfinding labels stay always-on | `Scripts/Runtime/ProximityLabel.cs` + contract test |
| 9 | No way to scroll back to earlier chatbot responses | ScrollRect scrollback; per-coach 24-turn history preserved and restored on reopen (LLM context stays 8 turns) | `Scripts/Runtime/NpcChatPanel.cs`, screenshot `chat-scrollback` |
| 10 | Some engineering decision-station objects not clickable | All 24 decision options verified reachable/clickable in editor measurement | Session cross-check (24/24) |

## Build state

- Desktop installers refreshed 2026-07-21 14:31: Quest APK (97.4 MiB) + Windows zip (74.3 MiB), both built from `20a93e8`.
- `20a93e8` pushed to `origin/codex/demo-pilot-validation`.

## Remaining (outside code)

- Quest physical-headset QA (sideload latest APK; check boundary, labels, chat in HMD)
- Pause menu is desktop-only by design — an in-VR menu is a separate task
- Real-play feel of chat scrolling
