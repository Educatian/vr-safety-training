using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Web budget for game audio: mono Vorbis, compressed in memory (VO + SFX + ambience ~2 MB instead of ~16 MB WAV).
    public sealed class WebAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/_Game/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = assetPath.Contains("/VO/");
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;      // 0.45 smeared noisy sounds (saw, rain, wind) into audible crackle
            importer.defaultSampleSettings = s;
        }
    }
}
