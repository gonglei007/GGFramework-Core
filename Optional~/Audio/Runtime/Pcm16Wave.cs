using System;
using System.IO;
using System.Text;

namespace GGFramework.Core.Audio
{
    /// <summary>Little-endian RIFF/WAVE with signed 16-bit interleaved samples.</summary>
    public static class Pcm16Wave
    {
        public static byte[] Encode(float[] samples, int count, int channels, int sampleRate)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (channels < 1 || channels > short.MaxValue / 2)
                throw new ArgumentOutOfRangeException(nameof(channels));
            int blockAlign = channels * 2;
            if (sampleRate < 1 || sampleRate > int.MaxValue / blockAlign)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            count = Math.Max(0, Math.Min(count, samples.Length));
            if (count % channels != 0)
                throw new ArgumentException("Sample count must contain complete frames.", nameof(count));
            int bytes = checked(count * 2);
            using (var stream = new MemoryStream(checked(44 + bytes)))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + bytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * blockAlign);
                writer.Write((short)blockAlign);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(bytes);
                for (int i = 0; i < count; i++)
                {
                    float sample = Math.Max(-1f, Math.Min(1f, samples[i]));
                    writer.Write(float.IsNaN(sample) ? (short)0 : (short)Math.Round(sample * 32767f));
                }
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] wave, out float[] samples, out int channels, out int sampleRate,
            int maxChannels = 2, int minSampleRate = 8000, int maxSampleRate = 96000, int maxSamples = 1572864)
        {
            samples = null;
            channels = sampleRate = 0;
            if (wave == null || wave.Length < 44 || Tag(wave, 0) != "RIFF" || Tag(wave, 8) != "WAVE") return false;
            long end = 8 + (long)Read32(wave, 4);
            if (end < 44 || end > wave.Length) return false;
            int formatChannels = 0, rate = 0, blockAlign = 0, start = 0, length = 0;
            for (long offset = 12; offset + 8 <= end;)
            {
                int chunk = (int)offset;
                int data = chunk + 8;
                uint size = Read32(wave, chunk + 4);
                long next = offset + 8 + size + (size & 1);
                if (next > end) return false;
                switch (Tag(wave, chunk))
                {
                    case "fmt ":
                        if (size < 16 || Read16(wave, data) != 1 || Read16(wave, data + 14) != 16) return false;
                        formatChannels = Read16(wave, data + 2);
                        rate = (int)Read32(wave, data + 4);
                        blockAlign = Read16(wave, data + 12);
                        break;
                    case "data":
                        if (start == 0) { start = data; length = (int)size; }
                        break;
                }
                offset = next;
            }
            if (start == 0 || formatChannels < 1 || formatChannels > maxChannels ||
                rate < minSampleRate || rate > maxSampleRate || blockAlign != formatChannels * 2 ||
                length < blockAlign || length % blockAlign != 0 || length / 2 > maxSamples) return false;
            samples = new float[length / 2];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = unchecked((short)Read16(wave, start + i * 2)) / 32768f;
            channels = formatChannels;
            sampleRate = rate;
            return true;
        }

        static string Tag(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);
        static int Read16(byte[] bytes, int offset) => bytes[offset] | bytes[offset + 1] << 8;
        static uint Read32(byte[] bytes, int offset) =>
            (uint)Read16(bytes, offset) | (uint)Read16(bytes, offset + 2) << 16;
    }
}
