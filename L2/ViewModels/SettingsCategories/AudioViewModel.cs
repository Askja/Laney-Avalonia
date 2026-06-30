using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class AudioViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> AudioDspModes { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AudioDspModeIds.Off, "Выкл"),
            new TwoStringTuple(AudioDspModeIds.Flat, "Ровный"),
            new TwoStringTuple(AudioDspModeIds.Normalize, "Нормализация"),
            new TwoStringTuple(AudioDspModeIds.VoiceClarity, "Чёткий голос"),
            new TwoStringTuple(AudioDspModeIds.Night, "Ночь"),
            new TwoStringTuple(AudioDspModeIds.BassBoost, "Больше баса")
        };

        public bool AudioPlayerLoop { get { return Settings.AudioPlayerLoop; } set { Settings.AudioPlayerLoop = value; OnPropertyChanged(); } }
        public TwoStringTuple CurrentAudioDspMode { get { return GetAudioDspMode(); } set { ChangeAudioDspMode(value); OnPropertyChanged(); } }
        public double AudioPlayerVolume { get { return Settings.AudioPlayerVolume; } set { Settings.AudioPlayerVolume = (int)Math.Round(value); OnPropertyChanged(); OnPropertyChanged(nameof(AudioPlayerVolumeLabel)); } }
        public string AudioPlayerVolumeLabel { get { return $"{Settings.AudioPlayerVolume}%"; } }
        public double AudioPlayerTrackRate { get { return Settings.AudioPlayerTrackRate / 100.0; } set { Settings.AudioPlayerTrackRate = ToRateX100(value); OnPropertyChanged(); OnPropertyChanged(nameof(AudioPlayerTrackRateLabel)); } }
        public string AudioPlayerTrackRateLabel { get { return FormatRate(Settings.AudioPlayerTrackRate); } }
        public double AudioPlayerPodcastRate { get { return Settings.AudioPlayerPodcastRate / 100.0; } set { Settings.AudioPlayerPodcastRate = ToRateX100(value); OnPropertyChanged(); OnPropertyChanged(nameof(AudioPlayerPodcastRateLabel)); } }
        public string AudioPlayerPodcastRateLabel { get { return FormatRate(Settings.AudioPlayerPodcastRate); } }
        public double AudioPlayerVoiceRate { get { return Settings.AudioPlayerVoiceRate / 100.0; } set { Settings.AudioPlayerVoiceRate = ToRateX100(value); OnPropertyChanged(); OnPropertyChanged(nameof(AudioPlayerVoiceRateLabel)); } }
        public string AudioPlayerVoiceRateLabel { get { return FormatRate(Settings.AudioPlayerVoiceRate); } }
        public double AudioPlayerSeekSeconds { get { return Settings.AudioPlayerSeekSeconds; } set { Settings.AudioPlayerSeekSeconds = (int)Math.Round(value); OnPropertyChanged(); OnPropertyChanged(nameof(AudioPlayerSeekSecondsLabel)); } }
        public string AudioPlayerSeekSecondsLabel { get { return $"{Settings.AudioPlayerSeekSeconds} сек"; } }
        public bool VoiceMessageResumeEnabled { get { return Settings.VoiceMessageResumeEnabled; } set { Settings.VoiceMessageResumeEnabled = value; OnPropertyChanged(); } }
        public bool VoiceMessageSkipSilence { get { return Settings.VoiceMessageSkipSilence; } set { Settings.VoiceMessageSkipSilence = value; OnPropertyChanged(); } }
        public bool LocalVoiceTranscriptionEnabled { get { return Settings.LocalVoiceTranscriptionEnabled; } set { Settings.LocalVoiceTranscriptionEnabled = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalVoiceTranscriptionModelPath {
            get { return Settings.LocalVoiceTranscriptionModelPath; }
            set {
                Settings.LocalVoiceTranscriptionModelPath = value;
                MarkCustomProfile();
                OnPropertyChanged();
                OnPropertyChanged(nameof(LocalVoiceTranscriptionModelPathSummary));
            }
        }
        public string LocalVoiceTranscriptionModelPathSummary { get { return String.IsNullOrWhiteSpace(Settings.LocalVoiceTranscriptionModelPath) ? "Не выбрана" : Settings.LocalVoiceTranscriptionModelPath; } }
        public string LocalVoiceTranscriptionLanguage { get { return Settings.LocalVoiceTranscriptionLanguage; } set { Settings.LocalVoiceTranscriptionLanguage = value; MarkCustomProfile(); OnPropertyChanged(); } }

        private TwoStringTuple GetAudioDspMode() {
            string id = Settings.AudioDspMode;
            return AudioDspModes.FirstOrDefault(m => m.Item1 == id) ?? AudioDspModes[0];
        }

        private void ChangeAudioDspMode(TwoStringTuple value) {
            if (value == null) return;

            Settings.AudioDspMode = value.Item1;
            AudioPlayerViewModel.ApplyAudioDspModeToInstances();
            OnPropertyChanged(nameof(CurrentAudioDspMode));
        }

        private static int ToRateX100(double value) {
            return (int)Math.Round(Math.Clamp(value, 0.5, 3.0) * 100);
        }

        private static string FormatRate(int rateX100) {
            return $"{(rateX100 / 100.0).ToString("0.##")}x";
        }

        private void MarkCustomProfile() {
            if (Settings.InterfaceProfile != InterfaceProfileIds.Custom) Settings.InterfaceProfile = InterfaceProfileIds.Custom;
        }
    }
}
