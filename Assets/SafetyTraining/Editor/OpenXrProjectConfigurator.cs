using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEditor.Build;
using UnityEngine.Rendering;

namespace SafetyTraining.Editor
{
    internal static class OpenXrProjectConfigurator
    {
        const string SettingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
        const string LoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

        public static bool ConfigureStandalone()
        {
            return Configure(BuildTargetGroup.Standalone, false);
        }

        public static bool ConfigureAndroid()
        {
            var assigned = Configure(BuildTargetGroup.Android, true);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.educatian.vrsafetytraining");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            AssetDatabase.SaveAssets();
            return assigned;
        }

        static bool Configure(BuildTargetGroup target, bool metaQuest)
        {
            SafetyScenePrimitives.EnsureFolder("Assets/XR/Settings");
            var perBuildTarget = LoadOrCreateSettings();
            if (!perBuildTarget.HasSettingsForBuildTarget(target))
                perBuildTarget.CreateDefaultSettingsForBuildTarget(target);
            if (!perBuildTarget.HasManagerSettingsForBuildTarget(target))
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(target);

            var general = perBuildTarget.SettingsForBuildTarget(target);
            general.InitManagerOnStart = true;
            var manager = perBuildTarget.ManagerSettingsForBuildTarget(target);
            var alreadyAssigned = manager.activeLoaders.Any(loader =>
                loader != null && loader.GetType().FullName == LoaderType);
            var assigned = alreadyAssigned || XRPackageMetadataStore.AssignLoader(manager, LoaderType, target);

            ConfigureControllerProfiles(target);
            if (metaQuest)
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
                if (settings != null)
                    Enable<MetaQuestFeature>(settings);
            }
            ApplyAutomaticValidationFixes(target);
            PlayerSettings.runInBackground = true;
            ConfigureTeleportInteractionLayer();

            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perBuildTarget);
            AssetDatabase.SaveAssets();
            if (!assigned)
                Debug.LogError($"OpenXR loader could not be assigned for {target} builds.");
            return assigned;
        }

        static void ConfigureControllerProfiles(BuildTargetGroup target)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
            if (settings == null)
                return;

            Enable<KHRSimpleControllerProfile>(settings);
            Enable<OculusTouchControllerProfile>(settings);
            Enable<MetaQuestTouchPlusControllerProfile>(settings);
            Enable<HTCViveControllerProfile>(settings);
            Enable<ValveIndexControllerProfile>(settings);
            Enable<EyeGazeInteraction>(settings);
            EditorUtility.SetDirty(settings);
        }

        static void Enable<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature != null)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
        }

        static void ApplyAutomaticValidationFixes(BuildTargetGroup target)
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, target);
            foreach (var issue in issues)
                if (issue.fixItAutomatic && issue.fixIt != null)
                    issue.fixIt.Invoke();
        }

        [MenuItem("Safety Training/Validate/OpenXR Standalone")]
        public static void ValidateStandalone()
        {
            ConfigureStandalone();
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Standalone);
            foreach (var issue in issues)
                Debug.LogWarning($"OpenXR validation: {issue.message}");
            Debug.Log($"OpenXR Standalone validation complete: {issues.Count} issue(s).");
        }

        [MenuItem("Safety Training/Validate/Meta Quest Android")]
        public static void ValidateMetaQuest()
        {
            ConfigureAndroid();
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            foreach (var issue in issues)
                Debug.LogWarning($"Meta Quest validation: {issue.message}");
            Debug.Log($"Meta Quest Android validation complete: {issues.Count} issue(s).");
        }

        static void ConfigureTeleportInteractionLayer()
        {
            var type = typeof(XRInteractionManager).Assembly.GetType(
                "UnityEngine.XR.Interaction.Toolkit.InteractionLayerSettings");
            var instance = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (instance == null)
                return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var isEmpty = type.GetMethod("IsLayerEmpty", flags);
            var setName = type.GetMethod("SetLayerNameAt", flags);
            if (isEmpty != null && setName != null && (bool)isEmpty.Invoke(instance, new object[] { 31 }))
            {
                setName.Invoke(instance, new object[] { 31, "Teleport" });
                EditorUtility.SetDirty((Object)instance);
                AssetDatabase.SaveAssets();
            }
        }

        static XRGeneralSettingsPerBuildTarget LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            return settings;
        }
    }
}
