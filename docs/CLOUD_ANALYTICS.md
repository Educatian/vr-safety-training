# Cloudflare analytics connection

## Live service

The Quest build sends pseudonymous safety-training telemetry to the existing Cloudflare research collector:

- Worker: `https://teacher-training-collector.jewoong-moon.workers.dev`
- D1 database: `teacher-training-research`
- Scenario partition: `vr-safety-training`
- Client ID: `teacher-training-quest`

The APK contains no Cloudflare account credential or administrator ingest token. At runtime it creates a random installation-scoped ID, requests a 24-hour signed Quest token, and keeps that token only in memory. Meta account identity, email, username, and device serial number are not collected.

## Local-first delivery

`CloudAnalyticsUploader` subscribes to the spatial event stream created by `TrainingEventLogger`. Every event is written first to `Application.persistentDataPath/SafetyTrainingCloudQueue`. The uploader registers the pseudonymous session and sends up to 50 events per batch. Queue files are deleted only after the Worker returns a successful response; offline or failed uploads retry after 15 seconds and on app focus.

On Quest, the queue is under:

`Android/data/com.educatian.vrsafetytraining/files/SafetyTrainingCloudQueue`

## Gaze semantics

The cloud record distinguishes:

- `eye_gaze`: Quest Pro OpenXR eye-gaze observation.
- `head_gaze_fallback`: Quest 2/3/3S camera-forward head-direction proxy.
- `gaze_target_glance`: target episode shorter than the dwell threshold.
- `gaze_target_dwell`: target episode meeting the dwell threshold.
- `gaze_mode_changed`: the active tracking source for the site.

Events include pseudonymous session ID, time, site and analytics zone, target ID/type, episode duration, player XYZ, hit XYZ, and tracking mode. The system does not upload eye images, pupil size, Meta identity, continuous head quaternion, or a raw biometric stream. `rawGazeConsent` remains `false` for this aggregate pipeline.

## Verification

On 2026-07-20 a live synthetic session passed through the production Worker and was queried from remote D1. D1 stored both an `eye_gaze` dwell event and a `head_gaze_fallback` glance event, and the session reached `completed` status. Unity EditMode coverage verifies endpoint policy, gaze field preservation, fallback labeling, and automatic uploader installation by `TrainingCoordinator`.

For a physical headset pilot, confirm that queued files disappear after connectivity returns, then query `training_events.event_json` by `scenario_id = 'vr-safety-training'` and the pseudonymous `session_id`.
