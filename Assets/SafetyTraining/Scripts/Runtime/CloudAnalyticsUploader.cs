using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace SafetyTraining.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CloudAnalyticsUploader : MonoBehaviour
    {
        const string InstallationIdKey = "SafetyTraining.CloudAnalytics.InstallationId";
        const float RetrySeconds = 15f;
        const int BatchSize = 50;

        TrainingEventLogger eventLogger;
        string authorization = string.Empty;
        string participantCode = string.Empty;
        bool syncInProgress;
        float nextRetryAt;

        public static CloudAnalyticsUploader EnsureOn(GameObject host, TrainingEventLogger logger)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            var uploader = host.GetComponent<CloudAnalyticsUploader>() ??
                           host.AddComponent<CloudAnalyticsUploader>();
            uploader.Initialize(logger);
            return uploader;
        }
        public void Initialize(TrainingEventLogger logger)
        {
            if (eventLogger == logger)
                return;
            if (eventLogger != null)
                eventLogger.SpatialEventRecorded -= OnSpatialEventRecorded;
            eventLogger = logger;
            if (eventLogger != null)
                eventLogger.SpatialEventRecorded += OnSpatialEventRecorded;
            TrySync();
        }

        void Update()
        {
            if (!syncInProgress && Time.unscaledTime >= nextRetryAt)
                TrySync();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                TrySync();
        }

        void OnDestroy()
        {
            if (eventLogger != null)
                eventLogger.SpatialEventRecorded -= OnSpatialEventRecorded;
        }

        void OnSpatialEventRecorded(TrainingEventLogger.SpatialAnalyticsEvent spatialEvent)
        {
#if !UNITY_WEBGL
            try
            {
                var cloudEvent = SafetyCloudEvent.FromSpatial(spatialEvent);
                var directory = QueueDirectory();
                Directory.CreateDirectory(directory);
                var fileName = cloudEvent.sessionId + "_" +
                               cloudEvent.sequence.ToString("D8") + ".json";
                File.WriteAllText(Path.Combine(directory, fileName),
                    JsonConvert.SerializeObject(cloudEvent), new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Cloud analytics queue write failed: " + exception.GetType().Name);
                return;
            }
            TrySync();
#endif
        }

        void TrySync()
        {
#if !UNITY_WEBGL
            if (syncInProgress || Time.unscaledTime < nextRetryAt || !HasPendingEvents())
                return;
            StartCoroutine(SyncPending());
#endif
        }

        IEnumerator SyncPending()
        {
            syncInProgress = true;
            if (string.IsNullOrEmpty(authorization))
            {
                var authorized = false;
                yield return RequestAuthorization(value => authorized = value);
                if (!authorized)
                {
                    FinishAttempt();
                    yield break;
                }
            }

            while (TryReadBatch(out var files, out var events))
            {
                var registered = false;
                yield return RegisterSession(events[0], value => registered = value);
                if (!registered)
                {
                    FinishAttempt();
                    yield break;
                }

                var uploaded = false;
                yield return UploadBatch(events, value => uploaded = value);
                if (!uploaded)
                {
                    FinishAttempt();
                    yield break;
                }
                foreach (var path in files)
                    TryDelete(path);
            }
            syncInProgress = false;
            nextRetryAt = Time.unscaledTime + RetrySeconds;
        }

        IEnumerator RequestAuthorization(Action<bool> completed)
        {
            var payload = new CloudQuestTokenRequest
            {
                installationId = InstallationId(),
                buildVersion = string.IsNullOrWhiteSpace(Application.version) ? "dev" : Application.version,
                deviceModel = DeviceModel()
            };
            using var request = JsonRequest("/v1/auth/quest-session", JsonConvert.SerializeObject(payload));
            request.SetRequestHeader("X-Client-Id", CloudAnalyticsSettings.ClientId);
            yield return request.SendWebRequest();
            if (!Accepted(request))
            {
                completed(false);
                yield break;
            }
            var response = JsonConvert.DeserializeObject<CloudQuestTokenResponse>(
                request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.token) ||
                string.IsNullOrWhiteSpace(response.participantCode))
            {
                completed(false);
                yield break;
            }
            authorization = response.token;
            participantCode = response.participantCode;
            completed(true);
        }

        IEnumerator RegisterSession(SafetyCloudEvent firstEvent, Action<bool> completed)
        {
            var payload = new CloudSessionStart
            {
                sessionId = firstEvent.sessionId,
                participantCode = participantCode,
                startedAtUtc = firstEvent.timestampUtc,
                deviceModel = DeviceModel(),
                buildVersion = string.IsNullOrWhiteSpace(Application.version) ? "dev" : Application.version,
                rawGazeConsent = false
            };
            using var request = AuthenticatedJsonRequest("/v1/sessions",
                JsonConvert.SerializeObject(payload), firstEvent.sessionId + "-start-v1");
            yield return request.SendWebRequest();
            completed(Accepted(request));
        }

        IEnumerator UploadBatch(SafetyCloudEvent[] events, Action<bool> completed)
        {
            var batch = new CloudEventBatch
            {
                requestId = events[0].sessionId + "-" + events[0].sequence + "-" +
                            events[events.Length - 1].sequence,
                events = events
            };
            var route = "/v1/sessions/" + events[0].sessionId + "/events";
            using var request = AuthenticatedJsonRequest(route,
                JsonConvert.SerializeObject(batch), batch.requestId);
            yield return request.SendWebRequest();
            completed(Accepted(request));
        }

        UnityWebRequest AuthenticatedJsonRequest(string route, string json, string idempotencyKey)
        {
            var request = JsonRequest(route, json);
            request.SetRequestHeader("Authorization", "Bearer " + authorization);
            request.SetRequestHeader("X-Client-Id", CloudAnalyticsSettings.ClientId);
            request.SetRequestHeader("Idempotency-Key", idempotencyKey);
            return request;
        }

        static UnityWebRequest JsonRequest(string route, string json)
        {
            var request = new UnityWebRequest(CloudAnalyticsSettings.Endpoint + route,
                UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;
            return request;
        }

        static bool Accepted(UnityWebRequest request)
        {
            var success = request.result == UnityWebRequest.Result.Success &&
                          request.responseCode >= 200 && request.responseCode < 300;
            if (!success)
                Debug.LogWarning("Cloud analytics retained for retry: " +
                                 request.responseCode + " " + request.error);
            return success;
        }

        static bool TryReadBatch(out string[] files, out SafetyCloudEvent[] events)
        {
            files = Array.Empty<string>();
            events = Array.Empty<SafetyCloudEvent>();
            try
            {
                var directory = QueueDirectory();
                if (!Directory.Exists(directory))
                    return false;
                var candidates = Directory.GetFiles(directory, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray();
                if (candidates.Length == 0)
                    return false;
                var first = JsonConvert.DeserializeObject<SafetyCloudEvent>(
                    File.ReadAllText(candidates[0], Encoding.UTF8));
                if (first == null)
                {
                    TryDelete(candidates[0]);
                    return TryReadBatch(out files, out events);
                }
                files = candidates.Where(path => Path.GetFileName(path)
                        .StartsWith(first.sessionId + "_", StringComparison.Ordinal))
                    .Take(BatchSize).ToArray();
                var loaded = new List<SafetyCloudEvent>(files.Length);
                foreach (var path in files)
                {
                    var item = JsonConvert.DeserializeObject<SafetyCloudEvent>(
                        File.ReadAllText(path, Encoding.UTF8));
                    if (item != null)
                        loaded.Add(item);
                }
                events = loaded.ToArray();
                return events.Length > 0;
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is JsonException)
            {
                Debug.LogWarning("Cloud analytics queue read failed: " + exception.GetType().Name);
                return false;
            }
        }

        static bool HasPendingEvents()
        {
            try
            {
                var directory = QueueDirectory();
                return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json").Any();
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        void FinishAttempt()
        {
            authorization = string.Empty;
            participantCode = string.Empty;
            syncInProgress = false;
            nextRetryAt = Time.unscaledTime + RetrySeconds;
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Cloud analytics queue cleanup failed: " + exception.GetType().Name);
            }
        }

        static string InstallationId()
        {
            var existing = PlayerPrefs.GetString(InstallationIdKey, string.Empty);
            if (Guid.TryParseExact(existing, "N", out _))
                return existing;
            var created = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(InstallationIdKey, created);
            PlayerPrefs.Save();
            return created;
        }

        static string DeviceModel()
        {
            return string.IsNullOrWhiteSpace(SystemInfo.deviceModel)
                ? "Unknown Quest device" : SystemInfo.deviceModel;
        }

        static string QueueDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "SafetyTrainingCloudQueue");
        }
    }
}
