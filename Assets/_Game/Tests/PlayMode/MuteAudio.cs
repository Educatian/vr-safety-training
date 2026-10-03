using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;

// Assembly-wide (no namespace): every PlayMode test runs silent. Automated play tests must never make sound
// on the machine running them; nothing here is persisted to the learner's settings.
[SetUpFixture]
public sealed class MuteAudioDuringTests
{
    private float volume;
    private bool paused;

    [OneTimeSetUp]
    public void Mute()
    {
        volume = AudioListener.volume; paused = AudioListener.pause;
        GameSettings.ForceMute = true;
        AudioListener.volume = 0f;
        AudioListener.pause = true;
    }

    [OneTimeTearDown]
    public void Restore()
    {
        GameSettings.ForceMute = false;
        AudioListener.volume = volume;
        AudioListener.pause = paused;
    }
}
