# Meta Quest deployment and QA

The same training scene now supports desktop preview, PC-connected IVR, and standalone Meta Quest. The lobby `DESKTOP / IVR` control changes input mode without exposing the five training sites outside the selected module.

## PC-connected IVR with Meta Quest Link

1. Start Meta Quest Link and set Meta Quest as the active OpenXR runtime.
2. Connect the headset, then run `Builds/Windows/VR-Safety-Training.exe`.
3. Select `IVR` in the lobby. If no OpenXR display is available, the app remains usable in Desktop mode and explains how to enable IVR.
4. Use Touch controllers for ray selection and grab/release; the desktop fallback remains WASD, right-mouse look, click, and drag.

The command-line override `-experienceMode ivr` is available for QA. `-experienceMode desktop` forces desktop preview on Windows.

## Standalone Quest APK

- Output: `Builds/MetaQuest/VR-Safety-Training-QuestPro-EyeGaze-Cloud-2026-07-20.apk`
- Validated size: 94,964,688 bytes (95.0 MB decimal)
- SHA-256: `92E04AB99D6907A26D606888E227A0551047353738D228FE81806BE2626E904A`
- Application ID: `com.educatian.vrsafetytraining`
- Runtime: OpenXR with Meta Quest Support, Touch profiles, and optional `XR_EXT_eye_gaze_interaction`
- Build: Android ARM64, IL2CPP, minimum API 29
- Graphics: Vulkan with OpenGLES3 fallback

Install the APK through Meta Quest Developer Hub or `adb install -r` on a developer-mode headset. Android starts directly in IVR; Desktop mode is intentionally unavailable there.

## Quest Pro eye tracking and fallback

On Quest Pro, enable eye tracking in the headset settings, complete calibration, and grant the app eye-tracking access when prompted. The runtime detects the OpenXR eye-gaze device and uses its tracked pose. If an eye device exists but momentarily loses a valid pose, sampling pauses instead of mixing head direction into the eye-gaze record.

The same APK supports Quest 3 and Quest 3S. Eye-tracking hardware is declared optional in the final Android manifest, so those headsets automatically use camera-forward head gaze. Each work-site visit records the active mode as `eye_gaze` or `head_gaze_fallback`.

Analytics are privacy-minimized. The app does not store eye images, pupil data, or raw biometric streams. It records meaningful target IDs, glance-versus-dwell duration, gaze mode, learner position, and target hit coordinates in the existing session JSONL. CSV export adds `gazeMode`, `targetKind`, and world/site-local hit coordinates for target attention and spatial heatmap analysis.

## Completed engineering gates

- Unity EditMode tests: 179/179 passed
- Windows OpenXR Project Validation: 0 issues
- Meta Quest Android OpenXR Project Validation: 0 issues
- Clean Windows player build: passed
- Clean Meta Quest release APK build: passed
- Windows no-runtime IVR request: returned safely to Desktop
- Desktop startup stability: zero rig-root, camera-position, and camera-angle drift over the observation window

## Physical-headset pilot gates

No Quest was connected over ADB during this validation, so physical Quest Pro permission, calibration, and eye-pose behavior remain hardware gates. Before learner deployment, verify:

1. Touch ray, direct grab, drag, release, portal selection, and NPC chat input.
2. Stable 72 Hz or higher frame timing at each site's heaviest view, with thermal and memory sampling.
3. Guardian/floor calibration, standing and seated reach, snap-turn/locomotion comfort, dominant-hand use, and UI legibility.
4. NPC/world collision containment and no wall penetration during long follow loops.
5. On Quest Pro, confirm the OS consent prompt, calibrated eye-pose targeting, gaze loss/recovery, and exported `eye_gaze` dwell events; on Quest 3/3S, confirm `head_gaze_fallback` events.
6. Five supervised formative learners before any unsupervised study.

This APK is an engineering pilot. It is not OSHA certification, proof of training efficacy, or a substitute for site-specific procedures and qualified-person instruction.


## Cloud analytics

The integrated Quest build uses a local-first queue and the live Cloudflare Worker documented in [CLOUD_ANALYTICS.md](CLOUD_ANALYTICS.md). Quest Pro events are labeled `eye_gaze`; Quest 2/3/3S events are labeled `head_gaze_fallback`. No raw eye image, pupil measurement, Meta identity, device serial, or continuous head quaternion is uploaded.
