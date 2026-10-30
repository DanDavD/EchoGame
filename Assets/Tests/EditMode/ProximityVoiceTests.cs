using System.Collections.Generic;
using EchoProtocol.EditorTools;
using EchoProtocol.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>Proximity voice chat: the codec, the jitter buffer and the microphone cursor, without a network.</summary>
    public class ProximityVoiceTests
    {
        // ---------------------------------------------------------------- codec

        [Test]
        public void MuLaw_RoundTrip_StaysCloseToTheOriginal()
        {
            for (float s = -1f; s <= 1f; s += 0.01f)
            {
                float back = VoiceCodec.DecodeSample(VoiceCodec.EncodeSample(s));
                // mu-law keeps about 2% relative precision on loud sounds and finer steps on quiet ones.
                Assert.That(back, Is.EqualTo(s).Within(0.002f + Mathf.Abs(s) * 0.035f), $"sample {s}");
            }
        }

        [Test]
        public void MuLaw_Silence_StaysSilent()
        {
            Assert.That(VoiceCodec.DecodeSample(VoiceCodec.EncodeSample(0f)), Is.EqualTo(0f).Within(0.0005f));
        }

        [Test]
        public void MuLaw_KeepsTheSign_AndClipsOutOfRange()
        {
            Assert.Greater(VoiceCodec.DecodeSample(VoiceCodec.EncodeSample(0.5f)), 0f);
            Assert.Less(VoiceCodec.DecodeSample(VoiceCodec.EncodeSample(-0.5f)), 0f);
            Assert.That(VoiceCodec.DecodeSample(VoiceCodec.EncodeSample(5f)), Is.EqualTo(1f).Within(0.03f));
        }

        [Test]
        public void Packet_EncodesAndDecodesWholeChunks()
        {
            var samples = new List<float> { 0f, 0.25f, -0.25f, 0.9f };
            byte[] packet = VoiceCodec.Encode(samples, 3);
            var decoded = new float[8];

            Assert.AreEqual(3, packet.Length, "one byte per sample");
            Assert.AreEqual(3, VoiceCodec.Decode(packet, decoded));
            Assert.That(decoded[1], Is.EqualTo(0.25f).Within(0.01f));

            // Test destination buffer overload (zero heap reallocations)
            var preallocated = new byte[8];
            int written = VoiceCodec.Encode(samples, 3, preallocated);
            Assert.AreEqual(3, written);
            Assert.AreEqual(packet[0], preallocated[0]);
            Assert.AreEqual(packet[1], preallocated[1]);
            Assert.AreEqual(packet[2], preallocated[2]);
        }

        [Test]
        public void Downsample_AveragesGroups_AndCarriesTheRest()
        {
            var output = new List<float>();
            var carry = new List<float>();

            VoiceCodec.Downsample(new[] { 0.2f, 0.4f, 0.6f }, 2, output, carry);
            Assert.AreEqual(1, output.Count);
            Assert.That(output[0], Is.EqualTo(0.3f).Within(0.0001f));
            Assert.AreEqual(1, carry.Count, "the odd sample waits for the next call");

            VoiceCodec.Downsample(new[] { 0.8f }, 2, output, carry);
            Assert.AreEqual(2, output.Count);
            Assert.That(output[1], Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void SamplesSince_HandlesTheClipWrappingAround()
        {
            Assert.AreEqual(100, VoiceCodec.SamplesSince(200, 300, 16000));
            Assert.AreEqual(150, VoiceCodec.SamplesSince(15900, 50, 16000), "the write head looped past the end");
            Assert.AreEqual(0, VoiceCodec.SamplesSince(-1, 50, 16000), "no cursor yet");
            Assert.Less(VoiceCodec.SamplesSince(10, 10, 16000), 16000);
        }

        // ---------------------------------------------------------------- jitter buffer

        [Test]
        public void Buffer_WaitsForThePrebuffer_BeforePlaying()
        {
            var buffer = new VoicePlaybackBuffer(capacity: 100, prebuffer: 10);
            var output = new float[4];

            buffer.Write(new[] { 1f, 1f, 1f, 1f, 1f }, 5);
            Assert.AreEqual(0, buffer.Read(output), "5 samples are not enough to start");
            Assert.AreEqual(0f, output[0]);

            buffer.Write(new[] { 1f, 1f, 1f, 1f, 1f }, 5);
            Assert.AreEqual(4, buffer.Read(output));
            Assert.AreEqual(1f, output[3]);
        }

        [Test]
        public void Buffer_RunningDry_PlaysSilence_AndWaitsForThePrebufferAgain()
        {
            var buffer = new VoicePlaybackBuffer(capacity: 100, prebuffer: 3);
            var output = new float[5];

            buffer.Write(new[] { 0.5f, 0.5f, 0.5f }, 3);
            Assert.AreEqual(3, buffer.Read(output), "only what was there");
            Assert.AreEqual(0f, output[4], "then silence");
            Assert.IsFalse(buffer.IsPlaying);

            buffer.Write(new[] { 0.5f }, 1);
            Assert.AreEqual(0, buffer.Read(output), "one sample does not restart playback");
        }

        [Test]
        public void Buffer_KeepsSamplesInOrder()
        {
            var buffer = new VoicePlaybackBuffer(capacity: 10, prebuffer: 1);
            var output = new float[3];

            buffer.Write(new[] { 0.1f, 0.2f, 0.3f }, 3);
            buffer.Read(output);

            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, output);
        }

        [Test]
        public void Buffer_Overflow_DropsTheOldestAudio()
        {
            var buffer = new VoicePlaybackBuffer(capacity: 3, prebuffer: 1);
            var output = new float[3];

            buffer.Write(new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, 5);

            Assert.AreEqual(3, buffer.Buffered);
            buffer.Read(output);
            CollectionAssert.AreEqual(new[] { 0.3f, 0.4f, 0.5f }, output, "the voice stays close to real time");
        }

        // ---------------------------------------------------------------- prefab

        [Test]
        public void PlayerPrefab_HasProximityVoice()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkRigBuilder.PlayerPrefabPath);
            Assert.IsNotNull(prefab.GetComponent<ProximityVoice>(),
                "the player prefab needs ProximityVoice (Echo Protocol > Build > Add Proximity Voice To Player)");
            Assert.IsNotNull(prefab.GetComponent<MicrophoneLoudness>(), "it reads this microphone");
        }
    }
}
