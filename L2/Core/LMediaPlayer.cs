using Avalonia.Threading;
using LibVLCSharp.Shared;
using Serilog;
using System;
using System.IO;

namespace ELOR.Laney.Core {
    public class LMediaPlayer {
        #region Static instances

        public static bool IsInitialized { get; private set; }
        private static LibVLC VLC;

        public static string LibVersion => VLC?.Version;
        public static string InitializationErrorReason { get; private set; }
        public static LMediaPlayer SFX { get; private set; }

        public static void InitStaticInstances() {
            try {
                SFX = new LMediaPlayer(nameof(SFX));
            } catch (VLCException vlcex) {
                SFX = null;
                InitializationErrorReason = vlcex.ToString();
                Log.Error(vlcex, $"LMediaPlayer could not be initialized! This is an error from libVLC side.");
            } catch (Exception ex) {
                SFX = null;
                Log.Error(ex, $"LMediaPlayer could not be initialized!");
            }
        }

        #endregion

        private Media _media;
        private MediaPlayer _player;

        public string InstanceName { get; private set; }
        public float Position { get; private set; }
        public bool IsPlaying { get; private set; }
        public float PlaybackRate { get; private set; } = 1f;
        public int VolumePercent { get; private set; } = 90;
        public string AudioDspMode { get; private set; } = AudioDspModeIds.Off;

        #region Events

        public event EventHandler MediaEnded;
        public event EventHandler<float> PositionChanged;
        public event EventHandler<bool> StateChanged;

        #endregion

        private static void VLC_Log(object sender, LogEventArgs e) {
            switch (e.Level) {
                case LogLevel.Error:
                    Log.Error($"libVLC: {e.Message}");
                    break;
                case LogLevel.Warning:
                    Log.Warning($"libVLC: {e.Message}");
                    break;
                case LogLevel.Notice:
                    Log.Information($"libVLC: {e.Message}");
                    break;
                case LogLevel.Debug:
                    Log.Verbose($"libVLC: {e.Message}");
                    break;
            }
        }

        public LMediaPlayer(string name) {
            InstanceName = name;
            if (IsInitialized) return;

            if (VLC == null) {
                VLC = new LibVLC(); // init VLC first time
                VLC.Log += VLC_Log;
            }

            IsInitialized = true;
        }

        public void PlayStream(Stream stream) {
            Media old = _player?.Media;

            _media = new Media(VLC, new StreamMediaInput(stream));
            if (_player == null) {
                _player = new MediaPlayer(_media);

                _player.PositionChanged += Player_PositionChanged;
                _player.Playing += Player_Playing;
                _player.Paused += Player_Paused;
                _player.EndReached += Player_EndReached;
            } else {
                _player.Media = _media;
            }

            old?.Dispose();

            _player.Play();
            ApplyPlaybackState();
        }

        public void PlayURL(string url) {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) return;
            PlayURL(new Uri(url));
        }

        public void PlayURL(Uri uri) {
            Media old = _player?.Media;

            _media = new Media(VLC, uri);
            if (_player == null) {
                _player = new MediaPlayer(_media);

                _player.PositionChanged += Player_PositionChanged;
                _player.Playing += Player_Playing;
                _player.Paused += Player_Paused;
                _player.EndReached += Player_EndReached;
            } else {
                _player.Media = _media;
            }

            old?.Dispose();

            _player.Play();
            ApplyPlaybackState();
        }

        public void Play() {
            if (_player != null && !_player.IsPlaying) {
                _player.Play();
                ApplyPlaybackState();
            }
        }

        public void Pause() {
            if (_player != null && _player.IsPlaying) _player.Pause();
        }

        public void SetPosition(TimeSpan position) {
            _player.SeekTo(position);
        }

        public void SetPlaybackRate(float rate) {
            PlaybackRate = Math.Clamp(rate, 0.5f, 3f);
            ApplyPlaybackRate();
        }

        public void SetVolume(int volumePercent) {
            VolumePercent = Math.Clamp(volumePercent, 0, 100);
            ApplyVolume();
        }

        public void SetAudioDspMode(string mode) {
            AudioDspMode = AudioDspModeIds.NormalizeMode(mode);
            ApplyEqualizer();
        }

        private void ApplyPlaybackState() {
            ApplyPlaybackRate();
            ApplyVolume();
            ApplyEqualizer();
        }

        private void ApplyPlaybackRate() {
            try {
                _player?.SetRate(PlaybackRate);
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to set playback rate {Rate} for {InstanceName}", PlaybackRate, InstanceName);
            }
        }

        private void ApplyVolume() {
            try {
                if (_player != null) _player.Volume = VolumePercent;
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to set volume {Volume} for {InstanceName}", VolumePercent, InstanceName);
            }
        }

        private void ApplyEqualizer() {
            if (_player == null) return;

            try {
                string mode = AudioDspModeIds.NormalizeMode(AudioDspMode);
                if (mode == AudioDspModeIds.Off) {
                    _player.UnsetEqualizer();
                    return;
                }

                Equalizer equalizer = BuildEqualizer(mode);
                try {
                    _player.SetEqualizer(equalizer);
                } finally {
                    (equalizer as IDisposable)?.Dispose();
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to apply audio DSP mode {Mode} for {InstanceName}", AudioDspMode, InstanceName);
            }
        }

        private static Equalizer BuildEqualizer(string mode) {
            Equalizer equalizer = new Equalizer();
            equalizer.SetPreamp(GetEqualizerPreamp(mode));

            uint bandCount = equalizer.BandCount;
            for (uint i = 0; i < bandCount; i++) {
                float frequency = equalizer.BandFrequency(i);
                equalizer.SetAmp(GetEqualizerAmp(mode, frequency), i);
            }

            return equalizer;
        }

        private static float GetEqualizerPreamp(string mode) {
            return mode switch {
                AudioDspModeIds.BassBoost => -3.5f,
                AudioDspModeIds.Night => -5.0f,
                AudioDspModeIds.Normalize => 1.5f,
                _ => 0f
            };
        }

        private static float GetEqualizerAmp(string mode, float frequency) {
            return mode switch {
                AudioDspModeIds.BassBoost => GetBassBoostAmp(frequency),
                AudioDspModeIds.VoiceClarity => GetVoiceClarityAmp(frequency),
                AudioDspModeIds.Night => GetNightAmp(frequency),
                AudioDspModeIds.Normalize => GetNormalizeAmp(frequency),
                _ => 0f
            };
        }

        private static float GetBassBoostAmp(float frequency) {
            if (frequency <= 125) return 6.0f;
            if (frequency <= 250) return 3.5f;
            if (frequency >= 8000) return 1.0f;
            return 0f;
        }

        private static float GetVoiceClarityAmp(float frequency) {
            if (frequency <= 125) return -4.0f;
            if (frequency >= 500 && frequency <= 4000) return 4.0f;
            if (frequency >= 8000) return -1.5f;
            return 0f;
        }

        private static float GetNightAmp(float frequency) {
            if (frequency <= 125) return -4.0f;
            if (frequency >= 500 && frequency <= 3000) return 2.0f;
            if (frequency >= 6000) return -3.0f;
            return 0f;
        }

        private static float GetNormalizeAmp(float frequency) {
            if (frequency <= 80) return -2.0f;
            if (frequency >= 250 && frequency <= 4000) return 1.5f;
            if (frequency >= 10000) return -1.0f;
            return 0f;
        }

        public void Dispose() {
            _media?.Dispose();
            _player?.Dispose();
        }

        #region Library Events

        private void Player_PositionChanged(object sender, MediaPlayerPositionChangedEventArgs e) {
            new Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Position = e.Position;
                    PositionChanged?.Invoke(this, e.Position);
                });
            })();
        }

        private void Player_Playing(object sender, EventArgs e) {
            new Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    IsPlaying = true;
                    StateChanged?.Invoke(this, true);
                });
            })();
        }

        private void Player_Paused(object sender, EventArgs e) {
            new Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    IsPlaying = false;
                    StateChanged?.Invoke(this, false);
                });
            })();
        }

        private void Player_EndReached(object sender, EventArgs e) {
            new Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    IsPlaying = false;
                    StateChanged?.Invoke(this, false);
                    MediaEnded?.Invoke(this, e);
                });
            })();
        }

        #endregion
    }
}
