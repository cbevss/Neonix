using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace Neonix.Services
{
    /// <summary>
    /// Audio visualiser input: listens to whatever is playing on the default output device
    /// (WASAPI loopback, never the microphone) and turns it into frequency bands.
    /// Nothing is recorded or stored; samples are analysed and discarded.
    /// </summary>
    public sealed class AudioService : IDisposable
    {
        public const int BandCount = 24;
        const int FftSize = 1024; // 2^10

        readonly object _lock = new();
        readonly float[] _bands = new float[BandCount];
        readonly float[] _window = new float[FftSize];
        readonly float[] _ring = new float[FftSize];
        readonly Complex[] _fft = new Complex[FftSize];
        WasapiLoopbackCapture? _capture;
        int _filled;
        float _peak = 0.001f;
        long _lastData;
        int _channels = 2, _sampleRate = 48000, _bits = 32;
        bool _isFloat = true;

        public AudioService()
        {
            for (int i = 0; i < FftSize; i++) _window[i] = (float)FastFourierTransform.HannWindow(i, FftSize);
        }

        public bool Active => _capture != null;

        public void SetActive(bool on)
        {
            if (on && _capture == null) Start();
            else if (!on && _capture != null) Stop();
        }

        void Start()
        {
            try
            {
                var cap = new WasapiLoopbackCapture();
                _channels = Math.Max(1, cap.WaveFormat.Channels);
                _sampleRate = cap.WaveFormat.SampleRate;
                _bits = cap.WaveFormat.BitsPerSample;
                _isFloat = cap.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat || _bits == 32;
                cap.DataAvailable += OnData;
                cap.StartRecording();
                _capture = cap;
            }
            catch
            {
                _capture?.Dispose();
                _capture = null;
            }
        }

        void Stop()
        {
            try
            {
                _capture?.StopRecording();
                _capture?.Dispose();
            }
            catch { }
            _capture = null;
            lock (_lock) { Array.Clear(_bands); }
        }

        void OnData(object? sender, WaveInEventArgs e)
        {
            int bytesPer = _bits / 8;
            int frame = bytesPer * _channels;
            if (frame <= 0) return;
            _lastData = Environment.TickCount64;

            for (int pos = 0; pos + frame <= e.BytesRecorded; pos += frame)
            {
                float sum = 0;
                for (int c = 0; c < _channels; c++)
                {
                    int o = pos + c * bytesPer;
                    sum += _isFloat && _bits == 32
                        ? BitConverter.ToSingle(e.Buffer, o)
                        : _bits == 16 ? BitConverter.ToInt16(e.Buffer, o) / 32768f : 0f;
                }
                _ring[_filled++] = sum / _channels;
                if (_filled == FftSize)
                {
                    Analyse();
                    _filled = 0;
                }
            }
        }

        void Analyse()
        {
            for (int i = 0; i < FftSize; i++)
            {
                _fft[i].X = _ring[i] * _window[i];
                _fft[i].Y = 0;
            }
            FastFourierTransform.FFT(true, 10, _fft);

            double binHz = (double)_sampleRate / FftSize;
            var raw = new float[BandCount];
            float max = 0;
            for (int b = 0; b < BandCount; b++)
            {
                // Log-spaced bands from 40 Hz to 16 kHz.
                double lo = 40 * Math.Pow(400, (double)b / BandCount);
                double hi = 40 * Math.Pow(400, (double)(b + 1) / BandCount);
                int i0 = Math.Clamp((int)(lo / binHz), 1, FftSize / 2 - 1);
                int i1 = Math.Clamp((int)(hi / binHz), i0, FftSize / 2 - 1);
                float sum = 0;
                for (int i = i0; i <= i1; i++) sum += (float)Math.Sqrt(_fft[i].X * _fft[i].X + _fft[i].Y * _fft[i].Y);
                raw[b] = sum / (i1 - i0 + 1);
                if (raw[b] > max) max = raw[b];
            }

            // Auto gain: follow the loudest recent band, forget slowly.
            _peak = Math.Max(max, _peak * 0.995f);
            _peak = Math.Max(_peak, 0.0005f);

            lock (_lock)
            {
                for (int b = 0; b < BandCount; b++)
                {
                    float v = (float)Math.Pow(Math.Clamp(raw[b] / _peak, 0, 1), 0.8);
                    _bands[b] = Math.Max(v, _bands[b] * 0.82f); // fast attack, slow release
                }
            }
        }

        /// <summary>Band levels 0..1, already scaled by the sensitivity setting.</summary>
        public float[] Bands()
        {
            float gain = 0.5f + Models.EffectSettingsAccess.Sensitivity() / 50f; // 0.5 .. 2.5
            bool stale = Environment.TickCount64 - _lastData > 250;

            var result = new float[BandCount];
            lock (_lock)
            {
                for (int b = 0; b < BandCount; b++)
                {
                    if (stale) _bands[b] *= 0.9f; // nothing playing: fade out
                    result[b] = Math.Clamp(_bands[b] * gain, 0, 1);
                }
            }
            return result;
        }

        public float Bass()
        {
            var b = Bands();
            return (b[0] + b[1] + b[2] + b[3]) / 4f;
        }

        public void Dispose() => Stop();
    }
}
