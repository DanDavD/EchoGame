using System;
using System.Collections.Generic;

namespace EchoProtocol.Voice
{
    /// <summary>
    /// Turns microphone samples into small network packets and back. The voice is downsampled (16 kHz to 8 kHz by
    /// default, phone quality, which suits a walkie-talkie horror game) and every sample is squeezed into one byte with
    /// G.711 mu-law, the same companding telephones use: quiet sounds keep their detail, loud ones lose a little.
    /// 8 kHz at one byte per sample is 64 kbit/s while someone talks, and nothing while they are silent.
    /// Pure rules: no Unity objects and no networking.
    /// </summary>
    public static class VoiceCodec
    {
        private const int Bias = 0x84;
        private const int Clip = 32635;

        /// <summary>Encodes one sample (-1 to 1) as a mu-law byte.</summary>
        public static byte EncodeSample(float sample)
        {
            int pcm = (int)(Math.Max(-1f, Math.Min(1f, sample)) * 32767f);
            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = -pcm;
            if (pcm > Clip) pcm = Clip;
            pcm += Bias;

            int exponent = 7;
            for (int mask = 0x4000; (pcm & mask) == 0 && exponent > 0; mask >>= 1) exponent--;

            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        /// <summary>Decodes a mu-law byte back to a sample (-1 to 1).</summary>
        public static float DecodeSample(byte encoded)
        {
            int value = ~encoded & 0xFF;
            int sign = value & 0x80;
            int exponent = (value >> 4) & 0x07;
            int mantissa = value & 0x0F;
            int pcm = (((mantissa << 3) + Bias) << exponent) - Bias;
            return (sign != 0 ? -pcm : pcm) / 32768f;
        }

        /// <summary>
        /// Averages every <paramref name="factor"/> consecutive samples of <paramref name="source"/> into one and appends it
        /// to <paramref name="destination"/>. The average is a crude low-pass filter, enough to keep speech clean.
        /// Leftover samples that do not fill a whole group are returned in <paramref name="carry"/> for the next call.
        /// </summary>
        public static void Downsample(IReadOnlyList<float> source, int factor, List<float> destination, List<float> carry)
        {
            factor = Math.Max(1, factor);
            for (int i = 0; i < source.Count; i++)
            {
                carry.Add(source[i]);
                if (carry.Count < factor) continue;

                float sum = 0f;
                for (int k = 0; k < factor; k++) sum += carry[k];
                destination.Add(sum / factor);
                carry.Clear();
            }
        }

        /// <summary>Encodes the first <paramref name="count"/> samples of <paramref name="samples"/> into a packet.</summary>
        public static byte[] Encode(IReadOnlyList<float> samples, int count)
        {
            count = Math.Min(count, samples.Count);
            var packet = new byte[count];
            for (int i = 0; i < count; i++) packet[i] = EncodeSample(samples[i]);
            return packet;
        }

        /// <summary>Encodes the first <paramref name="count"/> samples of <paramref name="samples"/> into <paramref name="destination"/>.</summary>
        public static int Encode(IReadOnlyList<float> samples, int count, byte[] destination)
        {
            if (destination == null) return 0;
            count = Math.Min(count, Math.Min(samples.Count, destination.Length));
            for (int i = 0; i < count; i++) destination[i] = EncodeSample(samples[i]);
            return count;
        }

        /// <summary>Decodes a packet into <paramref name="destination"/>, which must hold at least packet.Length samples.</summary>
        public static int Decode(byte[] packet, float[] destination)
        {
            if (packet == null) return 0;
            int count = Math.Min(packet.Length, destination.Length);
            for (int i = 0; i < count; i++) destination[i] = DecodeSample(packet[i]);
            return count;
        }

        /// <summary>
        /// Samples recorded between two positions of a looping microphone clip of <paramref name="clipLength"/> samples,
        /// when the write head moved from <paramref name="lastPosition"/> to <paramref name="position"/> (it may have
        /// wrapped around the end). Never more than one whole clip.
        /// </summary>
        public static int SamplesSince(int lastPosition, int position, int clipLength)
        {
            if (clipLength <= 0 || lastPosition < 0 || position < 0) return 0;
            int count = position >= lastPosition ? position - lastPosition : position + clipLength - lastPosition;
            return Math.Min(count, clipLength - 1);
        }
    }
}
