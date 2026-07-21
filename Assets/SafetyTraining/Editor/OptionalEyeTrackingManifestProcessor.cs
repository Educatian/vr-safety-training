using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace SafetyTraining.Editor
{
    public sealed class OptionalEyeTrackingManifestProcessor : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string EyeTrackingPermission = "com.oculus.permission.EYE_TRACKING";

        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string gradleProjectPath)
        {
            MakeEyeTrackingOptional(Path.Combine(gradleProjectPath, "xrmanifest.androidlib",
                "AndroidManifest.xml"));
        }

        public static void MakeEyeTrackingOptional(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"Eye-tracking manifest was not found at {manifestPath}.");
                return;
            }

            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);
            var manifest = document.DocumentElement;
            if (manifest == null)
                return;

            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace("android", AndroidNamespace);
            var eyeFeature = manifest.SelectSingleNode(
                "uses-feature[@android:name='oculus.software.eye_tracking']", namespaceManager) as XmlElement;
            if (eyeFeature != null)
                eyeFeature.SetAttribute("required", AndroidNamespace, "false");

            var permission = manifest.SelectSingleNode(
                "uses-permission[@android:name='com.oculus.permission.EYE_TRACKING']", namespaceManager);
            if (permission == null)
            {
                var element = document.CreateElement("uses-permission");
                element.SetAttribute("name", AndroidNamespace, EyeTrackingPermission);
                manifest.PrependChild(element);
            }

            document.Save(manifestPath);
            Debug.Log("Quest eye tracking configured as optional: Quest Pro uses eye gaze; " +
                      "Quest 3/3S retain head-gaze fallback.");
        }
    }
}
