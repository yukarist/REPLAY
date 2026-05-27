using NAudio.Wave;
using System;
using System.IO;

namespace REPLAY.Infrastructure.Audio
{
    public class AudioCaptureService
    {
        private WaveInEvent _waveIn;
        private MemoryStream _bufferStream = new();
        private DateTime _lastCutTime = DateTime.Now;
        private int _deviceNumber = 0;

        public event Action<float> OnLevelChanged;
        public event Action<byte[]> OnAudioChunkReady;
        public event Action<float>? OnLevelUpdated;

        private float _threshold = 0.08f;

        public void SetThreshold(float value)
        {
            _threshold = value;
        }

        private float _noiseReduction = 0.0f;

        public void SetNoiseReduction(float value)
        {
            _noiseReduction = value;
        }

        public void SetDevice(int deviceNumber)
        {
            _deviceNumber = deviceNumber;
        }

        public void Start()
        {
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                System.Diagnostics.Debug.WriteLine($"{i}: {caps.ProductName}");
            }

            // すでに動いている場合は一度止める
            Stop();

            _waveIn = new WaveInEvent
            {
                DeviceNumber = _deviceNumber,
                WaveFormat = new WaveFormat(16000, 1) // 16kHz モノラル (Whisper最適フォーマット)
            };

            System.Diagnostics.Debug.WriteLine($"使用デバイスインデックス: {_deviceNumber}");

            _waveIn.DataAvailable += (s, e) =>
            {
                float max = 0;

                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i);
                    float sample32 = Math.Abs(sample / 32768f);

                    if (sample32 > max)
                        max = sample32;
                }

                // 🔥 UIに通知
                OnLevelChanged?.Invoke(max);

                // ③ 無音カット
                if (max < _threshold)
                {
                    // 少しだけバッファに残す
                    return;
                }

                // ④ 書き戻し（ノイズ除去適用）
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i);

                    float sample32 = sample / 32768f;

                    if (Math.Abs(sample32) < _noiseReduction * 1.3f)
                    {
                        sample32 = 0;
                    }

                    short newSample = (short)(sample32 * 32768);

                    byte[] bytes = BitConverter.GetBytes(newSample);
                    e.Buffer[i] = bytes[0];
                    e.Buffer[i + 1] = bytes[1];
                }

                // ⑤ バッファ保存
                _bufferStream.Write(e.Buffer, 0, e.BytesRecorded);

                // ⑥ チャンク処理
                if ((DateTime.Now - _lastCutTime).TotalSeconds >= 2)
                {
                    var rawData = _bufferStream.ToArray();

                    if (rawData.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        var sourceFormat = new WaveFormat(16000, 1);

                        using var sourceStream = new RawSourceWaveStream(rawData, 0, rawData.Length, sourceFormat);
                        using var resampler = new MediaFoundationResampler(sourceStream, new WaveFormat(16000, 1))
                        {
                            ResamplerQuality = 60
                        };

                        WaveFileWriter.WriteWavFileToStream(ms, resampler);

                        OnAudioChunkReady?.Invoke(ms.ToArray());
                    }

                    _bufferStream.SetLength(0);
                    _lastCutTime = DateTime.Now;
                }
            };

            _waveIn.StartRecording();
        }

        public void Stop()
        {
            if (_waveIn != null)
            {
                try
                {
                    _waveIn.StopRecording();
                }
                catch { }
                finally
                {
                    _waveIn.Dispose();
                    _waveIn = null;
                }
            }
        }
    }
}