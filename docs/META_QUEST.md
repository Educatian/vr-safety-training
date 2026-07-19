# Meta Quest deployment and QA

The same training scene now supports desktop preview, PC-connected IVR, and standalone Meta Quest. The lobby `DESKTOP / IVR` control changes input mode without exposing the five training sites outside the selected module.

## PC-connected IVR with Meta Quest Link

1. Start Meta Quest Link and set Meta Quest as the active OpenXR runtime.
2. Connect the headset, then run `Builds/Windows/VR-Safety-Training.exe`.
3. Select `IVR` in the lobby. If no OpenXR display is available, the app remains usable in Desktop mode and explains how to enable IVR.
4. Use Touch controllers for ray selection and grab/release; the desktop fallback remains WASD, right-mouse look, click, and drag.

The command-line override `-experienceMode ivr` is available for QA. `-experienceMode desktop` forces desktop preview on Windows.

## Standalone Quest APK

- Output: `Builds/MetaQuest/VR-Safety-Training-Quest.apk`
- Validated size: 93,629,988 bytes (93.6 MB decimal)
- Application ID: `com.educatian.vrsafetytraining`
- Runtime: OpenXR with Meta Quest Support and Oculus/Meta Quest Touch profiles
- Build: Android ARM64, IL2CPP, minimum API 29
- Graphics: Vulkan with OpenGLES3 fallback

Install the APK through Meta Quest Developer Hub or `adb install -r` on a developer-mode headset. Android starts directly in IVR; Desktop mode is intentionally unavailable there.

## Completed engineering gates

- Unity EditMode tests: 124/124 passed
- Windows OpenXR Project Validation: 0 issues
- Meta Quest Android OpenXR Project Validation: 0 issues
- Clean Windows player build: passed
- Clean Meta Quest release APK build: passed
- Windows no-runtime IVR request: returned safely to Desktop
- Desktop startup stability: zero rig-root, camera-position, and camera-angle drift over the observation window

## Physical-headset pilot gates

The build has not yet been installed on a physical Quest in this validation environment. Before learner deployment, verify:

1. Touch ray, direct grab, drag, release, portal selection, and NPC chat input.
2. Stable 72 Hz or higher frame timing at each site's heaviest view, with thermal and memory sampling.
3. Guardian/floor calibration, standing and seated reach, snap-turn/locomotion comfort, dominant-hand use, and UI legibility.
4. NPC/world collision containment and no wall penetration during long follow loops.
5. Five supervised formative learners before any unsupervised study.

This APK is an engineering pilot. It is not OSHA certification, proof of training efficacy, or a substitute for site-specific procedures and qualified-person instruction.
