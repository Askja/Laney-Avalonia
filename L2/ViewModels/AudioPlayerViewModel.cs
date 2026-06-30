using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ELOR.Laney.ViewModels {
    public sealed class AudioEqualizerBandViewModel : ViewModelBase {
        private double _height;
        private string _ampLabel;

        public AudioEqualizerBandViewModel(string label) {
            Label = label;
        }

        public string Label { get; }
        public double Height { get { return _height; } private set { _height = value; OnPropertyChanged(); } }
        public string AmpLabel { get { return _ampLabel; } private set { _ampLabel = value; OnPropertyChanged(); } }

        public void Update(float amp) {
            float normalized = Math.Clamp(amp, -6f, 6f);
            Height = 8 + (normalized + 6f) / 12f * 30;
            AmpLabel = amp > 0.05f ? $"+{amp:0.#} dB" : amp < -0.05f ? $"{amp:0.#} dB" : "0 dB";
        }
    }

    public class AudioPlayerViewModel : ViewModelBase {
        private string _name;
        private ObservableCollection<AudioPlayerItem> _songs;
        private AudioPlayerItem _currentSong;
        private int _currentSongIndex;
        private TimeSpan _position;
        private bool _repeatOneSong;
        private bool _isPlaying;
        private bool _isTracklistDisplaying;
        private float _playbackRate = 1f;
        private int _volumePercent = 90;
        private int _seekSeconds = 15;
        private bool _canChangePlaybackRate;
        private DateTime _lastVoiceResumeSaveUtc = DateTime.MinValue;
        private long _lastVoiceResumePositionMs = -1;
        private DateTime _lastPlaybackHistorySaveUtc = DateTime.MinValue;
        private string _lastPlaybackHistoryKey;
        private long _lastPlaybackHistoryPositionMs = -1;
        private static readonly (float Frequency, string Label)[] EqualizerPreviewFrequencies = [
            (60, "60"),
            (125, "125"),
            (250, "250"),
            (1000, "1k"),
            (3000, "3k"),
            (6000, "6k"),
            (12000, "12k")
        ];

        private RelayCommand _playPauseCommand;
        private RelayCommand _getPreviousCommand;
        private RelayCommand _getNextCommand;
        private RelayCommand _seekBackwardCommand;
        private RelayCommand _seekForwardCommand;
        private RelayCommand _volumeDownCommand;
        private RelayCommand _volumeUpCommand;
        private RelayCommand _repeatCommand;
        private RelayCommand _cyclePlaybackRateCommand;
        private RelayCommand _resetPlaybackRateCommand;
        private RelayCommand _cycleAudioDspModeCommand;
        private RelayCommand _decreaseSeekStepCommand;
        private RelayCommand _increaseSeekStepCommand;
        private RelayCommand _shareCommand;
        private RelayCommand _openTracklistCommand;

        private LMediaPlayer Instance { get; set; }

        public string Name { get { return _name; } set { _name = value; OnPropertyChanged(); } }
        public ObservableCollection<AudioPlayerItem> Songs { get { return _songs; } private set { _songs = value; OnPropertyChanged(); } }
        public AudioPlayerItem CurrentSong { get { return _currentSong; } set { _currentSong = value; OnPropertyChanged(); OnPropertyChanged(nameof(QueueLabel)); OnPropertyChanged(nameof(PositionProgress)); } }
        public int CurrentSongIndex { get { return _currentSongIndex; } private set { _currentSongIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(QueueLabel)); } }
        public TimeSpan Position { get { return _position; } private set { _position = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionProgress)); PositionChanged?.Invoke(this, value); } }
        public bool RepeatOneSong { get { return _repeatOneSong; } set { _repeatOneSong = value; OnPropertyChanged(); } }
        public bool IsPlaying { get { return _isPlaying; } private set { _isPlaying = value; OnPropertyChanged(); } }
        public bool IsTracklistDisplaying { get { return _isTracklistDisplaying; } private set { _isTracklistDisplaying = value; OnPropertyChanged(); } }
        public float PlaybackRate { get { return _playbackRate; } private set { _playbackRate = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlaybackRateLabel)); } }
        public string PlaybackRateLabel { get { return $"{PlaybackRate.ToString("0.##", CultureInfo.InvariantCulture)}x"; } }
        public int VolumePercent { get { return _volumePercent; } set { SetVolume(value); } }
        public string VolumeLabel { get { return $"{VolumePercent}%"; } }
        public int SeekSeconds { get { return _seekSeconds; } private set { _seekSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(SeekBackwardLabel)); OnPropertyChanged(nameof(SeekForwardLabel)); } }
        public string SeekBackwardLabel { get { return $"-{SeekSeconds}s"; } }
        public string SeekForwardLabel { get { return $"+{SeekSeconds}s"; } }
        public string QueueLabel { get { return $"{CurrentSongIndex}/{Songs?.Count ?? 0}"; } }
        public ObservableCollection<TwoStringTuple> AudioDspModes { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AudioDspModeIds.Off, "Выкл"),
            new TwoStringTuple(AudioDspModeIds.Flat, "Ровный"),
            new TwoStringTuple(AudioDspModeIds.Normalize, "Нормализация"),
            new TwoStringTuple(AudioDspModeIds.VoiceClarity, "Чёткий голос"),
            new TwoStringTuple(AudioDspModeIds.Night, "Ночь"),
            new TwoStringTuple(AudioDspModeIds.BassBoost, "Больше баса")
        };
        public TwoStringTuple CurrentAudioDspMode { get { return GetAudioDspMode(); } set { ChangeAudioDspMode(value); } }
        public string AudioDspModeLabel { get { return CurrentAudioDspMode?.Item2 ?? "Выкл"; } }
        public string AudioDspShortLabel { get { return GetAudioDspShortLabel(Settings.AudioDspMode); } }
        public bool IsAudioDspEnabled { get { return Settings.AudioDspMode != AudioDspModeIds.Off; } }
        public ObservableCollection<AudioEqualizerBandViewModel> EqualizerPreviewBands { get; } = new ObservableCollection<AudioEqualizerBandViewModel>();
        public string EqualizerPreviewTitle { get { return $"EQ: {AudioDspModeLabel}"; } }
        public double PositionProgress {
            get {
                if (CurrentSong == null || CurrentSong.Duration.TotalMilliseconds <= 0) return 0;
                return Math.Clamp(Position.TotalMilliseconds / CurrentSong.Duration.TotalMilliseconds * 100.0, 0.0, 100.0);
            }
        }
        public bool CanChangePlaybackRate { get { return _canChangePlaybackRate; } private set { _canChangePlaybackRate = value; OnPropertyChanged(); } }

        public RelayCommand PlayPauseCommand { get { return _playPauseCommand; } private set { _playPauseCommand = value; OnPropertyChanged(); } }
        public RelayCommand GetPreviousCommand { get { return _getPreviousCommand; } private set { _getPreviousCommand = value; OnPropertyChanged(); } }
        public RelayCommand GetNextCommand { get { return _getNextCommand; } private set { _getNextCommand = value; OnPropertyChanged(); } }
        public RelayCommand SeekBackwardCommand { get { return _seekBackwardCommand; } private set { _seekBackwardCommand = value; OnPropertyChanged(); } }
        public RelayCommand SeekForwardCommand { get { return _seekForwardCommand; } private set { _seekForwardCommand = value; OnPropertyChanged(); } }
        public RelayCommand VolumeDownCommand { get { return _volumeDownCommand; } private set { _volumeDownCommand = value; OnPropertyChanged(); } }
        public RelayCommand VolumeUpCommand { get { return _volumeUpCommand; } private set { _volumeUpCommand = value; OnPropertyChanged(); } }
        public RelayCommand RepeatCommand { get { return _repeatCommand; } private set { _repeatCommand = value; OnPropertyChanged(); } }
        public RelayCommand CyclePlaybackRateCommand { get { return _cyclePlaybackRateCommand; } private set { _cyclePlaybackRateCommand = value; OnPropertyChanged(); } }
        public RelayCommand ResetPlaybackRateCommand { get { return _resetPlaybackRateCommand; } private set { _resetPlaybackRateCommand = value; OnPropertyChanged(); } }
        public RelayCommand CycleAudioDspModeCommand { get { return _cycleAudioDspModeCommand; } private set { _cycleAudioDspModeCommand = value; OnPropertyChanged(); } }
        public RelayCommand DecreaseSeekStepCommand { get { return _decreaseSeekStepCommand; } private set { _decreaseSeekStepCommand = value; OnPropertyChanged(); } }
        public RelayCommand IncreaseSeekStepCommand { get { return _increaseSeekStepCommand; } private set { _increaseSeekStepCommand = value; OnPropertyChanged(); } }
        public RelayCommand ShareCommand { get { return _shareCommand; } private set { _shareCommand = value; OnPropertyChanged(); } }
        public RelayCommand OpenTracklistCommand { get { return _openTracklistCommand; } private set { _openTracklistCommand = value; OnPropertyChanged(); } }

        public event EventHandler<TimeSpan> PositionChanged;
        public event EventHandler<bool> StateChanged;
        AudioType Type;
        private static readonly float[] PlaybackRates = [0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f, 3f];

        private AudioPlayerViewModel(List<Audio> songs, Audio currentSong, string name) {
            Log.Information($"APVM type=audio, count={songs.Count}, current={currentSong.Id}");
            Type = AudioType.Audio;
            Songs = new ObservableCollection<AudioPlayerItem>();
            Name = name;
            foreach (var song in songs) {
                if (song.Uri == null) continue;
                AudioPlayerItem api = new AudioPlayerItem(song);
                Songs.Add(api);
                if (song.Id == currentSong.Id) CurrentSong = api;
            }
            Initialize();

            SwitchSong(true);
            PropertyChanged += (a, b) => {
                if (b.PropertyName == nameof(CurrentSong)) SwitchSong();
            };
        }

        private AudioPlayerViewModel(List<Podcast> podcasts, Podcast currentPodcast, string name) {
            Log.Information($"APVM type=podcast, count={podcasts.Count}, current={currentPodcast.Id}");
            Type = AudioType.Podcast;
            Songs = new ObservableCollection<AudioPlayerItem>();
            Name = name;
            podcasts.ForEach(podcast => {
                AudioPlayerItem api = new AudioPlayerItem(podcast);
                Songs.Add(api);
                if (podcast.Id == currentPodcast.Id) CurrentSong = api;
            });
            Initialize();

            SwitchSong();
            PropertyChanged += (a, b) => {
                if (b.PropertyName == nameof(CurrentSong)) SwitchSong();
                if (b.PropertyName == nameof(RepeatOneSong)) Settings.AudioPlayerLoop = RepeatOneSong;
            };
        }

        private AudioPlayerViewModel(List<AudioMessage> messages, AudioMessage currentMessage, string ownerName) {
            Log.Information($"APVM type=voicemessage, count={messages.Count}, current={currentMessage.Id}");
            Type = AudioType.VoiceMessage;
            Songs = new ObservableCollection<AudioPlayerItem>();
            Name = ownerName;
            messages.ForEach(message => {
                AudioPlayerItem api = new AudioPlayerItem(message, ownerName);
                Songs.Add(api);
                if (message == currentMessage) CurrentSong = api;
            });
            Initialize();

            SwitchSong();
            PropertyChanged += (a, b) => {
                if (b.PropertyName == nameof(CurrentSong)) SwitchSong();
                if (b.PropertyName == nameof(RepeatOneSong)) Settings.AudioPlayerLoop = RepeatOneSong;
            };
        }

        private void Initialize() {
            Instance = new LMediaPlayer($"Audioplayer type: {Type}");
            RepeatOneSong = Type != AudioType.VoiceMessage ? Settings.AudioPlayerLoop : false;
            CanChangePlaybackRate = true;
            SeekSeconds = Settings.AudioPlayerSeekSeconds;
            SetVolume(Settings.AudioPlayerVolume, false);
            SetPlaybackRate(GetSavedPlaybackRate(), false);
            Instance.SetAudioDspMode(Settings.AudioDspMode);
            EnsureEqualizerPreviewBands();
            RefreshEqualizerPreview();
            Log.Information($"APVM initialized. Repeat={RepeatOneSong}");

            Instance.MediaEnded += Player_MediaEnded;
            Instance.PositionChanged += Instance_PositionChanged;
            Instance.StateChanged += Instance_StateChanged;
            PlayPauseCommand = new RelayCommand(o => {
                if (Instance.IsPlaying) {
                    Pause();
                } else {
                    Play();
                }
            });
            GetPreviousCommand = new RelayCommand(o => PlayPrevious());
            GetNextCommand = new RelayCommand(o => PlayNext());
            SeekBackwardCommand = new RelayCommand(o => SeekBy(TimeSpan.FromSeconds(-SeekSeconds)));
            SeekForwardCommand = new RelayCommand(o => SeekBy(TimeSpan.FromSeconds(SeekSeconds)));
            VolumeDownCommand = new RelayCommand(o => ChangeVolumeBy(-5));
            VolumeUpCommand = new RelayCommand(o => ChangeVolumeBy(5));
            RepeatCommand = new RelayCommand(o => {
                RepeatOneSong = !RepeatOneSong;
                Settings.AudioPlayerLoop = RepeatOneSong;
            });
            CyclePlaybackRateCommand = new RelayCommand(o => CyclePlaybackRate());
            ResetPlaybackRateCommand = new RelayCommand(o => SetPlaybackRate(1f));
            CycleAudioDspModeCommand = new RelayCommand(o => CycleAudioDspMode());
            DecreaseSeekStepCommand = new RelayCommand(o => ChangeSeekSecondsBy(-5));
            IncreaseSeekStepCommand = new RelayCommand(o => ChangeSeekSecondsBy(5));
            ShareCommand = new RelayCommand(o => { });
            OpenTracklistCommand = new RelayCommand(o => {
                IsTracklistDisplaying = !IsTracklistDisplaying;
            });
        }

        private void Instance_StateChanged(object sender, bool e) {
            IsPlaying = Instance.IsPlaying;
            if (!e) {
                MaybeSaveVoicePosition(true);
                MaybeSavePlaybackHistory(true);
            }
            StateChanged?.Invoke(this, e);
        }

        private void Instance_PositionChanged(object sender, float e) {
            if (CurrentSong == null) return;
            var millis = (float)CurrentSong.Duration.TotalMilliseconds / 1f * e;
            Position = TimeSpan.FromMilliseconds(millis);
            MaybeSaveVoicePosition();
            MaybeSavePlaybackHistory();
        }

        private void Player_MediaEnded(object sender, EventArgs e) {
            MaybeSavePlaybackHistory(true);
            if (Type == AudioType.VoiceMessage) ClearVoiceResumePosition();
            if (RepeatOneSong) {
                SetPosition(TimeSpan.Zero);
                Play();
                return;
            }

            int i = Songs.IndexOf(CurrentSong);
            if (i >= 0 && i < Songs.Count - 1) {
                CurrentSong = Songs[i + 1];
            } else if (Type == AudioType.Audio && Songs.Count > 0) {
                CurrentSong = Songs[0];
            }
        }

        private void Uninitialize() {
            Log.Information($"APVM uninitialized. Type={Type}");
            MaybeSaveVoicePosition(true);
            MaybeSavePlaybackHistory(true);
            Instance.MediaEnded -= Player_MediaEnded;
            Instance.PositionChanged -= Instance_PositionChanged;
            Instance.StateChanged -= Instance_StateChanged;
            Instance.Dispose();
        }

        private void SwitchSong(bool timeout = false) {
            if (CurrentSong == null) return;

            // Pause();
            Position = TimeSpan.FromMilliseconds(0);
            CurrentSongIndex = Songs.IndexOf(CurrentSong) + 1;
            Log.Information($"APVM changing audio to {CurrentSongIndex}.");
            _lastPlaybackHistoryKey = null;
            _lastPlaybackHistoryPositionMs = -1;
            MaybeSavePlaybackHistory(true);

            if (CurrentSong.Source != null) Instance.PlayURL(CurrentSong.Source);
            Instance.SetPlaybackRate(PlaybackRate);
            Instance.SetVolume(VolumePercent);
            Instance.SetAudioDspMode(Settings.AudioDspMode);
            ApplyVoiceStartPosition();
        }

        #region Controls

        public void SetPosition(TimeSpan position) {
            if (CurrentSong == null) return;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (position > CurrentSong.Duration) position = CurrentSong.Duration;
            Instance.SetPosition(position);
            Position = position;
            MaybeSaveVoicePosition(true);
            MaybeSavePlaybackHistory(true);
        }

        public void SeekBy(TimeSpan delta) {
            SetPosition(Position + delta);
        }

        public void Play() {
            if (Type != AudioType.VoiceMessage && VoiceMessageInstance != null) CloseVoiceMessageInstance();
            Instance.Play();
            MaybeSavePlaybackHistory(true);
        }

        public void Pause() {
            Instance.Pause();
            MaybeSaveVoicePosition(true);
            MaybeSavePlaybackHistory(true);
        }

        public void CyclePlaybackRate() {
            int index = Array.FindIndex(PlaybackRates, r => Math.Abs(r - PlaybackRate) < 0.01f);
            int nextIndex = index < 0 || index >= PlaybackRates.Length - 1 ? 0 : index + 1;
            SetPlaybackRate(PlaybackRates[nextIndex]);
        }

        public void SetPlaybackRate(float rate, bool persist = true) {
            rate = Math.Clamp(rate, 0.5f, 3f);
            PlaybackRate = rate;
            Instance.SetPlaybackRate(rate);
            if (persist) PersistPlaybackRate(rate);
        }

        public void SetVolume(int volume, bool persist = true) {
            volume = Math.Clamp(volume, 0, 100);
            if (_volumePercent == volume) {
                Instance?.SetVolume(volume);
                return;
            }

            _volumePercent = volume;
            OnPropertyChanged(nameof(VolumePercent));
            OnPropertyChanged(nameof(VolumeLabel));
            Instance?.SetVolume(volume);
            if (persist) Settings.AudioPlayerVolume = volume;
        }

        public void ChangeVolumeBy(int delta) {
            SetVolume(VolumePercent + delta);
        }

        public void ApplyAudioDspMode() {
            Instance?.SetAudioDspMode(Settings.AudioDspMode);
            RefreshEqualizerPreview();
            OnPropertyChanged(nameof(CurrentAudioDspMode));
            OnPropertyChanged(nameof(AudioDspModeLabel));
            OnPropertyChanged(nameof(AudioDspShortLabel));
            OnPropertyChanged(nameof(IsAudioDspEnabled));
            OnPropertyChanged(nameof(EqualizerPreviewTitle));
        }

        public void ApplyAudioSettingsFromSettings() {
            if (Type != AudioType.VoiceMessage) RepeatOneSong = Settings.AudioPlayerLoop;
            SeekSeconds = Settings.AudioPlayerSeekSeconds;
            SetVolume(Settings.AudioPlayerVolume, false);
            SetPlaybackRate(GetSavedPlaybackRate(), false);
            ApplyAudioDspMode();
        }

        public void ChangeSeekSecondsBy(int delta) {
            int seconds = Math.Clamp(SeekSeconds + delta, 5, 60);
            if (seconds == SeekSeconds) return;

            SeekSeconds = seconds;
            Settings.AudioPlayerSeekSeconds = seconds;
            ApplyAudioSettingsToInstances();
        }

        public void CycleAudioDspMode() {
            int index = AudioDspModes.IndexOf(CurrentAudioDspMode);
            int nextIndex = index < 0 || index >= AudioDspModes.Count - 1 ? 0 : index + 1;
            CurrentAudioDspMode = AudioDspModes[nextIndex];
        }

        public void PlayNext() {
            int i = Songs.IndexOf(CurrentSong);
            if (i >= Songs.Count - 1) {
                CurrentSong = Songs[0];
            } else {
                CurrentSong = Songs[i + 1];
            }
        }

        public void PlayPrevious() {
            int i = Songs.IndexOf(CurrentSong);
            if (i <= 0) {
                CurrentSong = Songs[Songs.Count - 1];
            } else {
                CurrentSong = Songs[i - 1];
            }
        }

        private float GetSavedPlaybackRate() {
            return Type switch {
                AudioType.Podcast => Settings.AudioPlayerPodcastRate / 100f,
                AudioType.VoiceMessage => Settings.AudioPlayerVoiceRate / 100f,
                _ => Settings.AudioPlayerTrackRate / 100f
            };
        }

        private TwoStringTuple GetAudioDspMode() {
            string id = Settings.AudioDspMode;
            return AudioDspModes.FirstOrDefault(m => m.Item1 == id) ?? AudioDspModes[0];
        }

        private void ChangeAudioDspMode(TwoStringTuple value) {
            if (value == null) return;

            Settings.AudioDspMode = value.Item1;
            ApplyAudioDspMode();
        }

        private static string GetAudioDspShortLabel(string mode) {
            return AudioDspModeIds.NormalizeMode(mode) switch {
                AudioDspModeIds.Off => "Off",
                AudioDspModeIds.Flat => "Flat",
                AudioDspModeIds.Normalize => "Norm",
                AudioDspModeIds.VoiceClarity => "Voice",
                AudioDspModeIds.Night => "Night",
                AudioDspModeIds.BassBoost => "Bass",
                _ => "DSP"
            };
        }

        private void EnsureEqualizerPreviewBands() {
            if (EqualizerPreviewBands.Count == EqualizerPreviewFrequencies.Length) return;

            EqualizerPreviewBands.Clear();
            foreach (var band in EqualizerPreviewFrequencies) {
                EqualizerPreviewBands.Add(new AudioEqualizerBandViewModel(band.Label));
            }
        }

        private void RefreshEqualizerPreview() {
            EnsureEqualizerPreviewBands();
            string mode = Settings.AudioDspMode;
            for (int i = 0; i < EqualizerPreviewFrequencies.Length; i++) {
                float amp = LMediaPlayer.GetEqualizerPreviewAmp(mode, EqualizerPreviewFrequencies[i].Frequency);
                EqualizerPreviewBands[i].Update(amp);
            }
        }

        private void PersistPlaybackRate(float rate) {
            int rateX100 = (int)Math.Round(rate * 100);
            switch (Type) {
                case AudioType.Podcast:
                    Settings.AudioPlayerPodcastRate = rateX100;
                    break;
                case AudioType.VoiceMessage:
                    Settings.AudioPlayerVoiceRate = rateX100;
                    break;
                default:
                    Settings.AudioPlayerTrackRate = rateX100;
                    break;
            }
        }

        private AudioMessage GetCurrentVoiceMessage() {
            return Type == AudioType.VoiceMessage ? CurrentSong?.Attachment as AudioMessage : null;
        }

        private void ApplyVoiceStartPosition() {
            AudioMessage message = GetCurrentVoiceMessage();
            if (message == null || CurrentSong == null) return;

            TimeSpan start = TimeSpan.Zero;
            if (Settings.VoiceMessageResumeEnabled) {
                long savedMs = Settings.GetVoiceMessageResumePositionMs(message.OwnerId, message.Id);
                if (savedMs > 1000 && savedMs < CurrentSong.Duration.TotalMilliseconds - 1500) {
                    start = TimeSpan.FromMilliseconds(savedMs);
                }
            }

            if (start == TimeSpan.Zero && Settings.VoiceMessageSkipSilence) {
                start = EstimateVoiceIntroSilence(message);
            }

            if (start > TimeSpan.Zero) SetPosition(start);
        }

        private TimeSpan EstimateVoiceIntroSilence(AudioMessage message) {
            if (message?.WaveForm == null || message.WaveForm.Length < 4 || CurrentSong == null) return TimeSpan.Zero;

            int max = message.WaveForm.Max();
            if (max <= 0) return TimeSpan.Zero;

            int threshold = Math.Max(1, (int)Math.Ceiling(max * 0.08));
            int firstAudibleIndex = Array.FindIndex(message.WaveForm, value => value >= threshold);
            if (firstAudibleIndex <= 0) return TimeSpan.Zero;

            double ms = CurrentSong.Duration.TotalMilliseconds / message.WaveForm.Length * firstAudibleIndex;
            if (ms < 300) return TimeSpan.Zero;

            return TimeSpan.FromMilliseconds(Math.Min(ms, 8000));
        }

        private void MaybeSaveVoicePosition(bool force = false) {
            if (!Settings.VoiceMessageResumeEnabled || CurrentSong == null) return;

            AudioMessage message = GetCurrentVoiceMessage();
            if (message == null) return;

            long currentMs = (long)Position.TotalMilliseconds;
            if (currentMs < 1000) return;
            if (currentMs >= CurrentSong.Duration.TotalMilliseconds - 1500) {
                ClearVoiceResumePosition();
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (!force && (now - _lastVoiceResumeSaveUtc).TotalSeconds < 5 && Math.Abs(currentMs - _lastVoiceResumePositionMs) < 1500) return;

            Settings.SetVoiceMessageResumePositionMs(message.OwnerId, message.Id, currentMs);
            _lastVoiceResumeSaveUtc = now;
            _lastVoiceResumePositionMs = currentMs;
        }

        private void ClearVoiceResumePosition() {
            AudioMessage message = GetCurrentVoiceMessage();
            if (message == null) return;

            Settings.SetVoiceMessageResumePositionMs(message.OwnerId, message.Id, 0);
            _lastVoiceResumePositionMs = 0;
        }

        private void MaybeSavePlaybackHistory(bool force = false) {
            if (CurrentSong == null) return;

            string key = BuildAudioHistoryKey(CurrentSong);
            if (String.IsNullOrWhiteSpace(key)) return;

            long positionMs = Math.Max(0, (long)Position.TotalMilliseconds);
            DateTime now = DateTime.UtcNow;
            if (!force
                && String.Equals(_lastPlaybackHistoryKey, key, StringComparison.OrdinalIgnoreCase)
                && (now - _lastPlaybackHistorySaveUtc).TotalSeconds < 10
                && Math.Abs(positionMs - _lastPlaybackHistoryPositionMs) < 5000) {
                return;
            }

            Settings.AddOrUpdateAudioPlaybackHistory(new AudioPlaybackHistoryItem {
                Key = key,
                Type = CurrentSong.Type.ToString(),
                OwnerId = CurrentSong.Attachment?.OwnerId ?? 0,
                Id = CurrentSong.Id,
                Title = CurrentSong.Title,
                Performer = CurrentSong.Performer,
                DurationMs = Math.Max(0, (long)CurrentSong.Duration.TotalMilliseconds),
                PositionMs = positionMs,
                UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            _lastPlaybackHistoryKey = key;
            _lastPlaybackHistorySaveUtc = now;
            _lastPlaybackHistoryPositionMs = positionMs;
        }

        private static string BuildAudioHistoryKey(AudioPlayerItem item) {
            if (item == null || item.Id == 0) return String.Empty;
            return $"{item.Type}:{item.Attachment?.OwnerId ?? 0}:{item.Id}";
        }

        #endregion

        #region Static members

        public static AudioPlayerViewModel MainInstance { get; private set; }
        public static AudioPlayerViewModel VoiceMessageInstance { get; private set; }

        public static event EventHandler InstancesChanged;

        public static void PlaySong(List<Audio> songs, Audio selectedSong, string name) {
            if (selectedSong.Uri == null || !LMediaPlayer.IsInitialized) return;

            CloseVoiceMessageInstance();
            MainInstance?.Uninitialize();
            MainInstance = new AudioPlayerViewModel(songs, selectedSong, name);
            InstancesChanged?.Invoke(null, null);
        }

        public static void PlayPodcast(List<Podcast> podcasts, Podcast selectedPodcast, string name) {
            if (selectedPodcast.Uri == null || !LMediaPlayer.IsInitialized) return;

            CloseVoiceMessageInstance();
            MainInstance?.Uninitialize();
            MainInstance = new AudioPlayerViewModel(podcasts, selectedPodcast, name);
            InstancesChanged?.Invoke(null, null);
        }

        public static void PlayVoiceMessage(List<AudioMessage> messages, AudioMessage selectedMessage, string ownerName) {
            if (selectedMessage.Uri == null || !LMediaPlayer.IsInitialized) return;

            VoiceMessageInstance?.Uninitialize();
            if (MainInstance != null) {
                MainInstance.Pause();
            }
            VoiceMessageInstance = new AudioPlayerViewModel(messages, selectedMessage, ownerName);
            InstancesChanged?.Invoke(null, null);
        }

        public static void CloseMainInstance() {
            if (MainInstance != null) {
                MainInstance.Uninitialize();
                MainInstance = null;
                InstancesChanged?.Invoke(null, null);
            }
        }

        public static void CloseVoiceMessageInstance() {
            if (VoiceMessageInstance != null) {
                VoiceMessageInstance.Uninitialize();
                VoiceMessageInstance = null;
                InstancesChanged?.Invoke(null, null);
            }
        }

        public static void ApplyAudioDspModeToInstances() {
            MainInstance?.ApplyAudioDspMode();
            VoiceMessageInstance?.ApplyAudioDspMode();
        }

        public static void ApplyAudioSettingsToInstances() {
            MainInstance?.ApplyAudioSettingsFromSettings();
            VoiceMessageInstance?.ApplyAudioSettingsFromSettings();
        }
        #endregion
    }
}
