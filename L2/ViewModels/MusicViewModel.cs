using ELOR.Laney.Core;
using ELOR.Laney.Core.Network;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VKUI.Controls;
using VkAudio = ELOR.VKAPILib.Objects.Audio;

namespace ELOR.Laney.ViewModels {
    public static class MusicSourceIds {
        public const string Library = "library";
        public const string Queue = "queue";
        public const string History = "history";
        public const string ChatAudio = "chat_audio";
    }

    public sealed class MusicTrackViewModel : ViewModelBase {
        private bool _isQueued;
        private bool _isCurrent;

        public MusicTrackViewModel(VkAudio audio, string sourceTitle) {
            Audio = audio;
            SourceTitle = sourceTitle;
        }

        public VkAudio Audio { get; }
        public string SourceTitle { get; }
        public string Title => String.IsNullOrWhiteSpace(Audio?.Title) ? "Без названия" : Audio.Title;
        public string Artist => String.IsNullOrWhiteSpace(Audio?.Artist) ? "Неизвестный исполнитель" : Audio.Artist;
        public string Subtitle => String.IsNullOrWhiteSpace(Audio?.Subtitle) ? SourceTitle : $"{SourceTitle} · {Audio.Subtitle}";
        public string DurationText => TimeSpan.FromSeconds(Math.Max(0, Audio?.Duration ?? 0)).ToString((Audio?.Duration ?? 0) >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
        public Uri CoverUri => Audio?.Thumb?.Uri;
        public Uri SourceUri => Audio?.Uri;
        public bool IsPlayable => SourceUri != null;
        public string Link => Audio == null || Audio.OwnerId == 0 || Audio.Id == 0 ? "https://vk.com/audio" : $"https://vk.com/audio{Audio.OwnerId}_{Audio.Id}";

        public bool IsQueued {
            get => _isQueued;
            set {
                if (_isQueued == value) return;
                _isQueued = value;
                OnPropertyChanged();
            }
        }

        public bool IsCurrent {
            get => _isCurrent;
            set {
                if (_isCurrent == value) return;
                _isCurrent = value;
                OnPropertyChanged();
            }
        }
    }

    public sealed class MusicEqualizerBandViewModel : ViewModelBase {
        private readonly Action<MusicEqualizerBandViewModel> changed;
        private double _gain;

        public MusicEqualizerBandViewModel(float frequency, string label, double gain, Action<MusicEqualizerBandViewModel> changed) {
            Frequency = frequency;
            Label = label;
            this.changed = changed;
            _gain = NormalizeGain(gain);
        }

        public float Frequency { get; }
        public string Label { get; }
        public double Gain {
            get => _gain;
            set {
                double normalized = NormalizeGain(value);
                if (Math.Abs(_gain - normalized) < 0.01) return;

                _gain = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GainLabel));
                OnPropertyChanged(nameof(FillHeight));
                changed?.Invoke(this);
            }
        }

        public string GainLabel => Gain > 0.05 ? $"+{Gain:0.#} dB" : Gain < -0.05 ? $"{Gain:0.#} dB" : "0 dB";
        public double FillHeight => 7 + (Gain + 12.0) / 24.0 * 24.0;

        private static double NormalizeGain(double value) {
            return Math.Round(Math.Clamp(value, -12.0, 12.0) * 2.0) / 2.0;
        }
    }

    public sealed class MusicViewModel : CommonViewModel {
        private const int PageSize = 100;
        private const long MaxEmbeddedCoverBytes = 2 * 1024 * 1024;
        private const string IntegrationsDirectoryName = "integrations";
        private const string ScrobbleQueueFileName = "music-scrobbles.jsonl";
        private const string ScrobbleExportFileName = "music-scrobbles-lastfm.tsv";

        private readonly VKSession session;
        private bool _initialized;
        private bool _hasMore = true;
        private int _offset;
        private string _query = String.Empty;
        private TwoStringTuple _currentSource;
        private TwoStringTuple _currentAudioDspMode;
        private string _statusText = "Готово";
        private string _downloadStatusText = "Скачивание сохраняет трек, sidecar JSON, ID3 и обложку, если VK отдал cover.";
        private AudioPlayerViewModel _currentPlayer;
        private bool _suppressCustomEqualizerApply;
        private string _lastDownloadedFilePath;
        private string _lastDownloadedCoverPath;
        private string _lastScrobbleExportPath;
        private int _scrobbleCount;
        private bool _lastDownloadTaggedWithId3;

        public MusicViewModel(VKSession session) {
            this.session = session;
            _currentSource = SourceOptions[0];
            _currentAudioDspMode = AudioDspModes.FirstOrDefault(m => m.Item1 == Settings.AudioDspMode) ?? AudioDspModes[0];
            CurrentPlayer = AudioPlayerViewModel.MainInstance;

            AudioPlayerViewModel.InstancesChanged += AudioPlayerViewModel_InstancesChanged;
            EnsureEqualizerPreviewBands();
            EnsureCustomEqualizerBands();
            RefreshScrobbleState();
            RefreshEqualizerPreview();
        }

        public ObservableCollection<MusicTrackViewModel> Tracks { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<MusicTrackViewModel> VisibleTracks { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<MusicTrackViewModel> Queue { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<MusicTrackViewModel> History { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<AudioEqualizerBandViewModel> EqualizerPreviewBands { get; } = new ObservableCollection<AudioEqualizerBandViewModel>();
        public ObservableCollection<MusicEqualizerBandViewModel> CustomEqualizerBands { get; } = new ObservableCollection<MusicEqualizerBandViewModel>();

        public ObservableCollection<TwoStringTuple> SourceOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(MusicSourceIds.Library, "Музыка VK"),
            new TwoStringTuple(MusicSourceIds.Queue, "Очередь"),
            new TwoStringTuple(MusicSourceIds.History, "История"),
            new TwoStringTuple(MusicSourceIds.ChatAudio, "Музыка из чатов")
        };

        public ObservableCollection<TwoStringTuple> AudioDspModes { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AudioDspModeIds.Off, "Выкл"),
            new TwoStringTuple(AudioDspModeIds.Flat, "Ровный"),
            new TwoStringTuple(AudioDspModeIds.Normalize, "Нормализация"),
            new TwoStringTuple(AudioDspModeIds.VoiceClarity, "Четкий голос"),
            new TwoStringTuple(AudioDspModeIds.Night, "Ночь"),
            new TwoStringTuple(AudioDspModeIds.BassBoost, "Больше баса"),
            new TwoStringTuple(AudioDspModeIds.Custom, "Свой EQ")
        };

        public TwoStringTuple CurrentSource {
            get => _currentSource;
            set {
                if (value == null || value.Item1 == _currentSource?.Item1) return;
                _currentSource = value;
                OnPropertyChanged();
                RefreshVisibleTracks();
            }
        }

        public TwoStringTuple CurrentAudioDspMode {
            get => _currentAudioDspMode;
            set {
                if (value == null || value.Item1 == _currentAudioDspMode?.Item1) return;
                _currentAudioDspMode = value;
                Settings.AudioDspMode = value.Item1;
                AudioPlayerViewModel.ApplyAudioDspModeToInstances();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AudioDspStatusText));
                RefreshEqualizerPreview();
            }
        }

        public string Query {
            get => _query;
            set {
                string normalized = value ?? String.Empty;
                if (_query == normalized) return;
                _query = normalized;
                OnPropertyChanged();
                RefreshVisibleTracks();
            }
        }

        public AudioPlayerViewModel CurrentPlayer {
            get => _currentPlayer;
            private set {
                if (_currentPlayer == value) return;
                if (_currentPlayer != null) _currentPlayer.PropertyChanged -= CurrentPlayer_PropertyChanged;
                _currentPlayer = value;
                if (_currentPlayer != null) _currentPlayer.PropertyChanged += CurrentPlayer_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCurrentPlayer));
                OnPropertyChanged(nameof(CurrentTrackTitle));
                OnPropertyChanged(nameof(CurrentTrackSubtitle));
                OnPropertyChanged(nameof(PlayerStatusText));
                RefreshPlaybackState();
            }
        }

        public bool HasCurrentPlayer => CurrentPlayer?.CurrentSong != null;
        public string CurrentTrackTitle => CurrentPlayer?.CurrentSong?.Title ?? "Ничего не играет";
        public string CurrentTrackSubtitle => CurrentPlayer?.CurrentSong == null ? "Выбери трек из списка или истории." : $"{CurrentPlayer.CurrentSong.Performer} · {CurrentPlayer.QueueLabel}";
        public string PlayerStatusText => CurrentPlayer == null ? "Плеер свободен" : $"{(CurrentPlayer.IsPlaying ? "Играет" : "Пауза")} · {CurrentPlayer.PlaybackRateLabel} · {CurrentPlayer.VolumeLabel}";
        public string AudioDspStatusText => $"EQ: {CurrentAudioDspMode?.Item2 ?? "Выкл"}";
        public string CustomEqualizerSummary => $"Свой EQ · {CustomEqualizerBands.Count} полос · -12/+12 dB";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        public string DownloadStatusText { get => _downloadStatusText; private set { _downloadStatusText = value; OnPropertyChanged(); } }
        public string QueueSummary => Queue.Count == 0 ? "Очередь пуста" : $"В очереди: {Queue.Count}";
        public string LibrarySummary => $"{VisibleTracks.Count} из {GetCurrentSourceTracks().Count()} · {(_hasMore ? "можно грузить еще" : "конец списка")}";
        public string IntegrationStatusText => _scrobbleCount == 0
            ? "VK status работает сразу. AIMP/Discord/Last.fm bridge ждут первый scrobble."
            : $"Scrobble bridge: {_scrobbleCount} записей · Last.fm/AIMP TSV готовится локально.";
        public string LastDownloadedFilePath => _lastDownloadedFilePath;
        public string LastDownloadedCoverPath => _lastDownloadedCoverPath;
        public string LastScrobbleExportPath => _lastScrobbleExportPath;
        public string IntegrationsDirectoryPath => GetIntegrationsDirectory();
        public bool LastDownloadTaggedWithId3 => _lastDownloadTaggedWithId3;
        public int ScrobbleCount => _scrobbleCount;
        public bool HasScrobbles => _scrobbleCount > 0;
        public bool HasTracks => VisibleTracks.Count > 0;
        public bool HasQueue => Queue.Count > 0;
        public bool CanLoadMore => !IsLoading && _hasMore && CurrentSource?.Item1 == MusicSourceIds.Library;
        public bool IsLoadingFirstPage => IsLoading && Tracks.Count == 0;

        public async Task InitializeAsync() {
            if (_initialized) return;
            _initialized = true;
            RefreshHistory();
            await RefreshAsync();
        }

        public async Task RefreshAsync() {
            Placeholder = null;
            _offset = 0;
            _hasMore = true;
            Tracks.Clear();
            if (CurrentSource?.Item1 == MusicSourceIds.ChatAudio) {
                LoadChatAudioTracks();
                RefreshVisibleTracks();
                return;
            }

            await LoadNextAsync();
        }

        public async Task LoadNextAsync() {
            if (IsLoading || !_hasMore) return;

            SetLoading(true);
            try {
                if (DemoMode.IsEnabled) {
                    LoadDemoTracks();
                    return;
                }

                await LoadApiPageAsync();
            } catch (Exception ex) {
                Log.Warning(ex, "Music page failed to load VK audio.");
                Placeholder = PlaceholderViewModel.GetForException(ex, _ => new System.Action(async () => await RefreshAsync())());
                _hasMore = false;
            } finally {
                SetLoading(false);
                RefreshVisibleTracks();
                UpdateEmptyState();
            }
        }

        public void PlayTrack(MusicTrackViewModel track) {
            if (track?.Audio?.Uri == null) return;
            if (!Queue.Any(q => IsSameTrack(q, track))) EnqueueTrack(track);

            List<VkAudio> songs = Queue.Count > 0
                ? Queue.Select(q => q.Audio).Where(a => a?.Uri != null).ToList()
                : VisibleTracks.Select(t => t.Audio).Where(a => a?.Uri != null).ToList();
            if (songs.Count == 0) return;

            AudioPlayerViewModel.PlaySong(songs, track.Audio, "Музыка VK");
            StatusText = $"Играет: {track.Artist} — {track.Title}";
            RefreshPlaybackState();
        }

        public void EnqueueTrack(MusicTrackViewModel track) {
            if (track?.Audio == null || Queue.Any(q => IsSameTrack(q, track))) return;

            Queue.Add(track);
            track.IsQueued = true;
            OnPropertyChanged(nameof(HasQueue));
            OnPropertyChanged(nameof(QueueSummary));
            OnPropertyChanged(nameof(LibrarySummary));
        }

        public void ClearQueue() {
            foreach (MusicTrackViewModel track in Queue) track.IsQueued = false;
            Queue.Clear();
            OnPropertyChanged(nameof(HasQueue));
            OnPropertyChanged(nameof(QueueSummary));
            OnPropertyChanged(nameof(LibrarySummary));
        }

        public void ResetCustomEqualizer() {
            _suppressCustomEqualizerApply = true;
            try {
                foreach (MusicEqualizerBandViewModel band in CustomEqualizerBands) band.Gain = 0;
            } finally {
                _suppressCustomEqualizerApply = false;
            }

            ApplyCustomEqualizerBands();
        }

        public async Task DownloadTrackAsync(MusicTrackViewModel track) {
            if (track?.Audio?.Uri == null) return;

            _lastDownloadedFilePath = null;
            _lastDownloadedCoverPath = null;
            _lastDownloadTaggedWithId3 = false;
            OnPropertyChanged(nameof(LastDownloadedFilePath));
            OnPropertyChanged(nameof(LastDownloadedCoverPath));
            OnPropertyChanged(nameof(LastDownloadTaggedWithId3));

            string directory = Path.Combine(App.LocalDataPath, "music-downloads");
            Directory.CreateDirectory(directory);
            string extension = Path.GetExtension(track.Audio.Uri.LocalPath);
            if (String.IsNullOrWhiteSpace(extension) || extension.Length > 8) extension = ".mp3";

            string fileName = SanitizeFileName($"{track.Artist} - {track.Title}{extension}");
            string target = Path.Combine(directory, fileName);
            int duplicate = 1;
            while (File.Exists(target)) {
                target = Path.Combine(directory, SanitizeFileName($"{track.Artist} - {track.Title} ({duplicate++}){extension}"));
            }

            try {
                if (track.Audio.Uri.IsFile) {
                    File.Copy(track.Audio.Uri.LocalPath, target, false);
                } else {
                    using HttpResponseMessage response = await LNet.GetAsync(track.Audio.Uri);
                    response.EnsureSuccessStatusCode();
                    await using FileStream output = File.Create(target);
                    await response.Content.CopyToAsync(output);
                }

                string coverPath = await SaveTrackCoverAsync(track, target);
                bool id3Tagged = await TryWriteId3TagAsync(target, track, coverPath);
                await File.WriteAllTextAsync(target + ".json", BuildTrackSidecar(track, target, coverPath, id3Tagged));

                _lastDownloadedFilePath = target;
                _lastDownloadedCoverPath = coverPath;
                _lastDownloadTaggedWithId3 = id3Tagged;
                OnPropertyChanged(nameof(LastDownloadedFilePath));
                OnPropertyChanged(nameof(LastDownloadedCoverPath));
                OnPropertyChanged(nameof(LastDownloadTaggedWithId3));

                string coverStatus = coverPath == null ? "cover нет" : "cover сохранен";
                DownloadStatusText = $"Скачано: {Path.GetFileName(target)} · {(id3Tagged ? "ID3 OK" : "ID3 пропущен")} · {coverStatus}";
            } catch (Exception ex) {
                DownloadStatusText = $"Не скачалось: {ex.Message}";
            }
        }

        public async Task SetVkStatusFromCurrentAsync() {
            MusicTrackViewModel track = GetCurrentTrackFromPlayer();
            if (track == null || session == null || DemoMode.IsEnabled) {
                StatusText = "Для VK status нужен реальный аккаунт и текущий трек.";
                return;
            }

            string text = $"🎧 {track.Artist} — {track.Title}";
            try {
                using JsonDocument _ = await session.API.CallMethodAsync("status.set", new Dictionary<string, string> {
                    { "text", text }
                });
                StatusText = $"VK status: {text}";
            } catch (Exception ex) {
                StatusText = $"VK status не применился: {ex.Message}";
            }
        }

        public async Task ScrobbleCurrentAsync() {
            MusicTrackViewModel track = GetCurrentTrackFromPlayer();
            if (track == null) {
                StatusText = "Нечего скробблить.";
                return;
            }

            await AppendScrobbleAsync(track);
            StatusText = $"Scrobble: {track.Artist} — {track.Title}";
        }

        public async Task ExportScrobblesAsync() {
            List<MusicScrobbleEntry> entries = ReadScrobbleEntries();
            if (entries.Count == 0) {
                StatusText = "Scrobble-очередь пустая, экспортировать нечего.";
                RefreshScrobbleState();
                return;
            }

            string directory = GetIntegrationsDirectory();
            Directory.CreateDirectory(directory);
            string exportPath = Path.Combine(directory, ScrobbleExportFileName);
            await File.WriteAllTextAsync(exportPath, BuildLastFmCompatibleScrobbleExport(entries));
            _lastScrobbleExportPath = exportPath;
            OnPropertyChanged(nameof(LastScrobbleExportPath));
            StatusText = $"Scrobble export: {entries.Count} записей · {Path.GetFileName(exportPath)}";
        }

        public void ClearScrobbles() {
            TryDeleteFile(GetScrobbleQueuePath());
            TryDeleteFile(Path.Combine(GetIntegrationsDirectory(), ScrobbleExportFileName));
            _lastScrobbleExportPath = null;
            RefreshScrobbleState();
            OnPropertyChanged(nameof(LastScrobbleExportPath));
            StatusText = "Scrobble-очередь очищена.";
        }

        public bool OpenIntegrationsFolder() {
            string directory = GetIntegrationsDirectory();
            Directory.CreateDirectory(directory);
            return Launcher.LaunchFolder(directory);
        }

        private async Task AppendScrobbleAsync(MusicTrackViewModel track) {
            if (track?.Audio == null) return;

            string directory = GetIntegrationsDirectory();
            Directory.CreateDirectory(directory);
            MusicScrobbleEntry entry = new MusicScrobbleEntry {
                Service = "local",
                Type = "music.scrobble",
                Artist = track.Artist,
                Title = track.Title,
                Album = String.IsNullOrWhiteSpace(track.Audio.Subtitle) ? track.SourceTitle : track.Audio.Subtitle,
                OwnerId = track.Audio.OwnerId,
                Id = track.Audio.Id,
                Duration = Math.Max(0, track.Audio.Duration),
                Link = track.Link,
                At = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string line = JsonSerializer.Serialize(new {
                service = entry.Service,
                type = entry.Type,
                artist = entry.Artist,
                title = entry.Title,
                album = entry.Album,
                owner_id = entry.OwnerId,
                id = entry.Id,
                duration = entry.Duration,
                link = entry.Link,
                at = entry.At
            });
            await File.AppendAllTextAsync(GetScrobbleQueuePath(), line + Environment.NewLine);
            RefreshScrobbleState();
        }

        private void RefreshScrobbleState() {
            _scrobbleCount = CountScrobbleEntries();
            OnPropertyChanged(nameof(ScrobbleCount));
            OnPropertyChanged(nameof(HasScrobbles));
            OnPropertyChanged(nameof(IntegrationStatusText));
        }

        private int CountScrobbleEntries() {
            string path = GetScrobbleQueuePath();
            if (!File.Exists(path)) return 0;

            try {
                int count = 0;
                foreach (string line in File.ReadLines(path)) {
                    if (!String.IsNullOrWhiteSpace(line)) count++;
                }
                return count;
            } catch {
                return 0;
            }
        }

        private List<MusicScrobbleEntry> ReadScrobbleEntries() {
            string path = GetScrobbleQueuePath();
            if (!File.Exists(path)) return new List<MusicScrobbleEntry>();

            List<MusicScrobbleEntry> entries = new List<MusicScrobbleEntry>();
            foreach (string line in File.ReadLines(path)) {
                MusicScrobbleEntry entry = TryReadScrobbleEntry(line);
                if (entry != null) entries.Add(entry);
            }
            return entries;
        }

        private static MusicScrobbleEntry TryReadScrobbleEntry(string line) {
            if (String.IsNullOrWhiteSpace(line)) return null;

            try {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                string artist = ReadString(root, "artist");
                string title = ReadString(root, "title");
                if (String.IsNullOrWhiteSpace(artist) || String.IsNullOrWhiteSpace(title)) return null;

                return new MusicScrobbleEntry {
                    Service = ReadString(root, "service") ?? "local",
                    Type = ReadString(root, "type") ?? "music.scrobble",
                    Artist = artist,
                    Title = title,
                    Album = ReadString(root, "album"),
                    OwnerId = ReadInt64(root, "owner_id"),
                    Id = (int)ReadInt64(root, "id"),
                    Duration = (int)ReadInt64(root, "duration"),
                    Link = ReadString(root, "link"),
                    At = ReadInt64(root, "at")
                };
            } catch {
                return null;
            }
        }

        private static string BuildLastFmCompatibleScrobbleExport(IEnumerable<MusicScrobbleEntry> entries) {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("timestamp\tartist\ttrack\talbum\tduration\tservice\tvk_url");
            foreach (MusicScrobbleEntry entry in entries.OrderBy(e => e.At)) {
                builder.Append(entry.At).Append('\t')
                    .Append(EscapeTsv(entry.Artist)).Append('\t')
                    .Append(EscapeTsv(entry.Title)).Append('\t')
                    .Append(EscapeTsv(entry.Album)).Append('\t')
                    .Append(entry.Duration).Append('\t')
                    .Append(EscapeTsv(entry.Service)).Append('\t')
                    .Append(EscapeTsv(entry.Link))
                    .AppendLine();
            }
            return builder.ToString();
        }

        private static string EscapeTsv(string value) {
            return (value ?? String.Empty)
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        private static string ReadString(JsonElement root, string propertyName) {
            return root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static long ReadInt64(JsonElement root, string propertyName) {
            if (!root.TryGetProperty(propertyName, out JsonElement value)) return 0;
            return value.ValueKind switch {
                JsonValueKind.Number when value.TryGetInt64(out long result) => result,
                JsonValueKind.String when Int64.TryParse(value.GetString(), out long result) => result,
                _ => 0
            };
        }

        private static string GetIntegrationsDirectory() {
            return Path.Combine(App.LocalDataPath, IntegrationsDirectoryName);
        }

        private static string GetScrobbleQueuePath() {
            return Path.Combine(GetIntegrationsDirectory(), ScrobbleQueueFileName);
        }

        public async Task<MusicExportQaReport> RunExportQaAsync(bool cleanup = false) {
            await InitializeAsync();
            MusicTrackViewModel track = VisibleTracks.FirstOrDefault(t => t.IsPlayable)
                ?? Tracks.FirstOrDefault(t => t.IsPlayable);
            if (track == null) {
                return new MusicExportQaReport {
                    Passed = false,
                    Reason = "no_playable_track"
                };
            }

            await DownloadTrackAsync(track);

            string filePath = LastDownloadedFilePath;
            string sidecarPath = filePath == null ? null : filePath + ".json";
            bool fileExists = File.Exists(filePath);
            bool sidecarExists = File.Exists(sidecarPath);
            bool isMp3 = filePath != null && Path.GetExtension(filePath).Equals(".mp3", StringComparison.OrdinalIgnoreCase);
            bool id3Header = !isMp3 || await FileStartsWithId3Async(filePath);
            bool coverExists = LastDownloadedCoverPath != null && File.Exists(LastDownloadedCoverPath);
            await AppendScrobbleAsync(track);
            await ExportScrobblesAsync();
            bool scrobbleExportExists = LastScrobbleExportPath != null && File.Exists(LastScrobbleExportPath);

            MusicExportQaReport report = new MusicExportQaReport {
                Passed = fileExists && sidecarExists && id3Header && (!isMp3 || LastDownloadTaggedWithId3) && scrobbleExportExists && ScrobbleCount > 0,
                Reason = DownloadStatusText,
                FilePath = filePath,
                FileBytes = fileExists ? new FileInfo(filePath).Length : 0,
                SidecarExists = sidecarExists,
                Id3Header = id3Header,
                Id3Tagged = LastDownloadTaggedWithId3,
                CoverExists = coverExists,
                CoverBytes = coverExists ? new FileInfo(LastDownloadedCoverPath).Length : 0,
                ScrobbleExportExists = scrobbleExportExists,
                ScrobbleExportBytes = scrobbleExportExists ? new FileInfo(LastScrobbleExportPath).Length : 0,
                ScrobbleCount = ScrobbleCount
            };

            if (cleanup) CleanupExportQaFiles(filePath, sidecarPath, LastDownloadedCoverPath, LastScrobbleExportPath, GetScrobbleQueuePath());
            return report;
        }

        public void Dispose() {
            AudioPlayerViewModel.InstancesChanged -= AudioPlayerViewModel_InstancesChanged;
            if (_currentPlayer != null) _currentPlayer.PropertyChanged -= CurrentPlayer_PropertyChanged;
        }

        private async Task LoadApiPageAsync() {
            Dictionary<string, string> parameters = new Dictionary<string, string> {
                { "count", PageSize.ToString() },
                { "offset", _offset.ToString() }
            };

            using JsonDocument document = await session.API.CallMethodAsync("audio.get", parameters);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("error", out JsonElement error)) {
                string message = error.TryGetProperty("error_msg", out JsonElement errorMessage)
                    ? errorMessage.GetString()
                    : "VK API вернул ошибку audio.get.";
                throw new InvalidOperationException(message);
            }

            if (!root.TryGetProperty("response", out JsonElement response)
                || !response.TryGetProperty("items", out JsonElement items)
                || items.ValueKind != JsonValueKind.Array) {
                _hasMore = false;
                return;
            }

            int loaded = 0;
            foreach (JsonElement item in items.EnumerateArray()) {
                VkAudio audio = (VkAudio)JsonSerializer.Deserialize(item.GetRawText(), typeof(VkAudio), BuildInJsonContext.Default);
                if (audio == null || audio.Id == 0) continue;

                AddOrUpdateTrack(new MusicTrackViewModel(audio, "VK audio.get"));
                loaded++;
            }

            _offset += loaded;
            _hasMore = loaded >= PageSize;
        }

        private void LoadDemoTracks() {
            LoadChatAudioTracks();
            if (!Tracks.Any(t => t.IsPlayable)) {
                string samplePath = EnsureDemoAudioSamplePath();
                Tracks.Add(new MusicTrackViewModel(new VkAudio {
                    Id = 1,
                    OwnerId = session?.Id ?? 0,
                    Title = "Laney demo beat",
                    Artist = "Laney",
                    Duration = 38,
                    Url = File.Exists(samplePath) ? new Uri(samplePath).AbsoluteUri : null
                }, "demo"));
            }

            _hasMore = false;
        }

        private void LoadChatAudioTracks() {
            if (DemoMode.IsEnabled) {
                foreach (Message message in DemoMode.GetDemoSessionById(session.Id)?.Messages ?? Enumerable.Empty<Message>()) {
                    AddAudioAttachments(message?.Attachments);
                    foreach (Message forwarded in message?.ForwardedMessages ?? Enumerable.Empty<Message>()) AddAudioAttachments(forwarded?.Attachments);
                }
            } else {
                foreach (MessageViewModel message in session?.CurrentOpenedChat?.DisplayedMessages ?? Enumerable.Empty<MessageViewModel>()) {
                    AddAudioAttachments(message.Attachments);
                    foreach (Message forwarded in message.ForwardedMessages ?? Enumerable.Empty<Message>()) AddAudioAttachments(forwarded?.Attachments);
                }
            }

            _hasMore = false;
        }

        private void AddAudioAttachments(IEnumerable<Attachment> attachments) {
            foreach (Attachment attachment in attachments ?? Enumerable.Empty<Attachment>()) {
                if (attachment.Type != AttachmentType.Audio || attachment.Audio == null) continue;
                AddOrUpdateTrack(new MusicTrackViewModel(attachment.Audio, "из чатов"));
            }
        }

        private void RefreshHistory() {
            History.Clear();
            foreach (AudioPlaybackHistoryItem item in Settings.GetAudioPlaybackHistory()) {
                MusicTrackViewModel match = Tracks.FirstOrDefault(t => t.Audio.OwnerId == item.OwnerId && t.Audio.Id == item.Id);
                if (match != null) {
                    History.Add(match);
                    continue;
                }

                History.Add(new MusicTrackViewModel(new VkAudio {
                    Id = item.Id,
                    OwnerId = item.OwnerId,
                    Artist = item.Performer,
                    Title = item.Title,
                    Duration = (int)Math.Round(item.DurationMs / 1000.0)
                }, $"история · {FormatUpdatedAt(item.UpdatedAtUnix)}"));
            }
        }

        private void RefreshVisibleTracks() {
            VisibleTracks.Clear();
            IEnumerable<MusicTrackViewModel> source = GetCurrentSourceTracks();
            string query = Query?.Trim();
            if (!String.IsNullOrWhiteSpace(query)) {
                source = source.Where(track => $"{track.Artist} {track.Title} {track.Subtitle}".Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            foreach (MusicTrackViewModel track in source.Take(300)) VisibleTracks.Add(track);

            RefreshPlaybackState();
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(LibrarySummary));
            OnPropertyChanged(nameof(CanLoadMore));
            OnPropertyChanged(nameof(IsLoadingFirstPage));
        }

        private IEnumerable<MusicTrackViewModel> GetCurrentSourceTracks() {
            return CurrentSource?.Item1 switch {
                MusicSourceIds.Queue => Queue,
                MusicSourceIds.History => History,
                MusicSourceIds.ChatAudio => Tracks.Where(t => String.Equals(t.SourceTitle, "из чатов", StringComparison.OrdinalIgnoreCase)),
                _ => Tracks
            };
        }

        private void AddOrUpdateTrack(MusicTrackViewModel track) {
            if (track?.Audio == null) return;
            if (Tracks.Any(t => IsSameTrack(t, track))) return;

            track.IsQueued = Queue.Any(q => IsSameTrack(q, track));
            Tracks.Add(track);
        }

        private void RefreshPlaybackState() {
            int currentId = CurrentPlayer?.CurrentSong?.Id ?? 0;
            long currentOwnerId = CurrentPlayer?.CurrentSong?.Attachment?.OwnerId ?? 0;
            foreach (MusicTrackViewModel track in Tracks.Concat(History).Concat(Queue).Distinct()) {
                track.IsCurrent = currentId != 0 && track.Audio.Id == currentId && (currentOwnerId == 0 || track.Audio.OwnerId == currentOwnerId);
            }
        }

        private MusicTrackViewModel GetCurrentTrackFromPlayer() {
            int currentId = CurrentPlayer?.CurrentSong?.Id ?? 0;
            long currentOwnerId = CurrentPlayer?.CurrentSong?.Attachment?.OwnerId ?? 0;
            if (currentId == 0) return null;

            return Tracks.Concat(Queue).Concat(History)
                .FirstOrDefault(track => track.Audio.Id == currentId && (currentOwnerId == 0 || track.Audio.OwnerId == currentOwnerId));
        }

        private static bool IsSameTrack(MusicTrackViewModel left, MusicTrackViewModel right) {
            if (left?.Audio == null || right?.Audio == null) return false;
            return left.Audio.Id == right.Audio.Id && left.Audio.OwnerId == right.Audio.OwnerId;
        }

        private void EnsureEqualizerPreviewBands() {
            if (EqualizerPreviewBands.Count > 0) return;

            foreach ((float _, string label) in LMediaPlayer.CustomEqualizerBands) {
                EqualizerPreviewBands.Add(new AudioEqualizerBandViewModel(label));
            }
        }

        private void EnsureCustomEqualizerBands() {
            if (CustomEqualizerBands.Count > 0) return;

            float[] gains = LMediaPlayer.GetCustomEqualizerGains();
            for (int i = 0; i < LMediaPlayer.CustomEqualizerBands.Length; i++) {
                (float frequency, string label) = LMediaPlayer.CustomEqualizerBands[i];
                double gain = i < gains.Length ? gains[i] : 0;
                CustomEqualizerBands.Add(new MusicEqualizerBandViewModel(frequency, label, gain, OnCustomEqualizerBandChanged));
            }
        }

        private void OnCustomEqualizerBandChanged(MusicEqualizerBandViewModel band) {
            if (_suppressCustomEqualizerApply) return;
            ApplyCustomEqualizerBands();
        }

        private void ApplyCustomEqualizerBands() {
            Settings.AudioDspCustomGains = LMediaPlayer.FormatCustomEqualizerGains(CustomEqualizerBands.Select(b => (float)b.Gain).ToArray());
            if (Settings.AudioDspMode != AudioDspModeIds.Custom) {
                Settings.AudioDspMode = AudioDspModeIds.Custom;
                _currentAudioDspMode = AudioDspModes.FirstOrDefault(m => m.Item1 == AudioDspModeIds.Custom) ?? AudioDspModes[0];
                OnPropertyChanged(nameof(CurrentAudioDspMode));
                OnPropertyChanged(nameof(AudioDspStatusText));
            }

            AudioPlayerViewModel.ApplyAudioDspModeToInstances();
            RefreshEqualizerPreview();
        }

        private void RefreshEqualizerPreview() {
            for (int i = 0; i < EqualizerPreviewBands.Count && i < LMediaPlayer.CustomEqualizerBands.Length; i++) {
                EqualizerPreviewBands[i].Update(LMediaPlayer.GetEqualizerPreviewAmp(Settings.AudioDspMode, LMediaPlayer.CustomEqualizerBands[i].Frequency));
            }
        }

        private void SetLoading(bool value) {
            IsLoading = value;
            OnPropertyChanged(nameof(IsLoadingFirstPage));
            OnPropertyChanged(nameof(CanLoadMore));
        }

        private void UpdateEmptyState() {
            if (VisibleTracks.Count > 0 || Placeholder != null) return;

            Placeholder = new PlaceholderViewModel {
                Icon = new VKIcon { Id = VKIconNames.Icon56InfoOutline },
                Header = "Музыка не найдена",
                Text = "Для выбранного источника пока нет треков. Можно открыть историю, чат-аудио или обновить VK audio.",
                ActionButton = "Обновить",
                ActionButtonFunc = new RelayCommand(_ => new System.Action(async () => await RefreshAsync())())
            };
        }

        private void AudioPlayerViewModel_InstancesChanged(object sender, EventArgs e) {
            CurrentPlayer = AudioPlayerViewModel.MainInstance;
        }

        private void CurrentPlayer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AudioPlayerViewModel.CurrentSong)
                || e.PropertyName == nameof(AudioPlayerViewModel.IsPlaying)
                || e.PropertyName == nameof(AudioPlayerViewModel.PlaybackRate)
                || e.PropertyName == nameof(AudioPlayerViewModel.VolumePercent)) {
                OnPropertyChanged(nameof(CurrentTrackTitle));
                OnPropertyChanged(nameof(CurrentTrackSubtitle));
                OnPropertyChanged(nameof(PlayerStatusText));
                RefreshPlaybackState();
            }
        }

        private static string SanitizeFileName(string name) {
            string value = String.IsNullOrWhiteSpace(name) ? "track.mp3" : name;
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Trim();
        }

        private static string EnsureDemoAudioSamplePath() {
            string directory = Path.Combine(App.LocalDataPath, "demo-assets");
            string target = Path.Combine(directory, "bb2.mp3");
            if (File.Exists(target)) return target;

            try {
                Directory.CreateDirectory(directory);
                using Stream input = AssetsManager.OpenAsset(new Uri("avares://laney/Assets/Audio/bb2.mp3"));
                using FileStream output = File.Create(target);
                input.CopyTo(output);
                return target;
            } catch {
                return target;
            }
        }

        private static async Task<string> SaveTrackCoverAsync(MusicTrackViewModel track, string audioPath) {
            Uri coverUri = track?.CoverUri;
            if (coverUri == null || String.IsNullOrWhiteSpace(audioPath)) return null;

            string coverPath = $"{Path.Combine(Path.GetDirectoryName(audioPath) ?? String.Empty, Path.GetFileNameWithoutExtension(audioPath))}.cover{ResolveCoverExtension(coverUri)}";
            try {
                if (coverUri.IsFile) {
                    FileInfo source = new FileInfo(coverUri.LocalPath);
                    if (!source.Exists || source.Length > MaxEmbeddedCoverBytes) return null;

                    File.Copy(source.FullName, coverPath, true);
                    return coverPath;
                }

                using HttpResponseMessage response = await LNet.GetAsync(coverUri);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxEmbeddedCoverBytes) return null;

                await using Stream input = await response.Content.ReadAsStreamAsync();
                await using (FileStream output = File.Create(coverPath)) {
                    await input.CopyToAsync(output);
                }

                FileInfo saved = new FileInfo(coverPath);
                if (!saved.Exists || saved.Length == 0 || saved.Length > MaxEmbeddedCoverBytes) {
                    TryDeleteFile(coverPath);
                    return null;
                }

                return coverPath;
            } catch {
                TryDeleteFile(coverPath);
                return null;
            }
        }

        private static async Task<bool> TryWriteId3TagAsync(string audioPath, MusicTrackViewModel track, string coverPath) {
            if (String.IsNullOrWhiteSpace(audioPath)
                || !Path.GetExtension(audioPath).Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(audioPath)) {
                return false;
            }

            string tempPath = audioPath + ".id3tmp";
            try {
                byte[] coverBytes = null;
                string coverMime = null;
                if (!String.IsNullOrWhiteSpace(coverPath) && File.Exists(coverPath)) {
                    FileInfo cover = new FileInfo(coverPath);
                    if (cover.Length > 0 && cover.Length <= MaxEmbeddedCoverBytes) {
                        coverBytes = await File.ReadAllBytesAsync(coverPath);
                        coverMime = ResolveCoverMime(coverPath);
                    }
                }

                byte[] tag = BuildId3v23Tag(track, coverBytes, coverMime);
                if (tag == null || tag.Length == 0) return false;

                await using (FileStream source = File.Open(audioPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    long skip = TryGetExistingId3TagLength(source);
                    source.Position = Math.Min(skip, source.Length);

                    await using (FileStream destination = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                        await destination.WriteAsync(tag);
                        await source.CopyToAsync(destination);
                    }
                }

                File.Move(tempPath, audioPath, true);
                return true;
            } catch {
                TryDeleteFile(tempPath);
                return false;
            }
        }

        private static byte[] BuildId3v23Tag(MusicTrackViewModel track, byte[] coverBytes, string coverMime) {
            if (track == null) return null;

            using MemoryStream body = new MemoryStream();
            WriteFrame(body, BuildTextFrame("TIT2", track.Title));
            WriteFrame(body, BuildTextFrame("TPE1", track.Artist));
            WriteFrame(body, BuildTextFrame("TALB", String.IsNullOrWhiteSpace(track.Audio?.Subtitle) ? track.SourceTitle : track.Audio.Subtitle));
            WriteFrame(body, BuildTextFrame("TCON", "VK"));
            WriteFrame(body, BuildTextFrame("TXXX", $"Laney VK URL: {track.Link}"));

            if (coverBytes != null && coverBytes.Length > 0 && !String.IsNullOrWhiteSpace(coverMime)) {
                WriteFrame(body, BuildApicFrame(coverBytes, coverMime));
            }

            byte[] bodyBytes = body.ToArray();
            using MemoryStream tag = new MemoryStream();
            tag.Write(Encoding.ASCII.GetBytes("ID3"));
            tag.WriteByte(3);
            tag.WriteByte(0);
            tag.WriteByte(0);
            tag.Write(EncodeSyncSafeSize(bodyBytes.Length));
            tag.Write(bodyBytes);
            return tag.ToArray();
        }

        private static byte[] BuildTextFrame(string frameId, string value) {
            if (String.IsNullOrWhiteSpace(value)) return null;

            byte[] text = Encoding.Unicode.GetBytes(value.Trim());
            byte[] payload = new byte[text.Length + 3];
            payload[0] = 1;
            payload[1] = 0xFF;
            payload[2] = 0xFE;
            Buffer.BlockCopy(text, 0, payload, 3, text.Length);
            return BuildFrame(frameId, payload);
        }

        private static byte[] BuildApicFrame(byte[] coverBytes, string mime) {
            byte[] mimeBytes = Encoding.ASCII.GetBytes(mime);
            byte[] payload = new byte[1 + mimeBytes.Length + 1 + 1 + 1 + coverBytes.Length];
            int offset = 0;
            payload[offset++] = 0;
            Buffer.BlockCopy(mimeBytes, 0, payload, offset, mimeBytes.Length);
            offset += mimeBytes.Length;
            payload[offset++] = 0;
            payload[offset++] = 3;
            payload[offset++] = 0;
            Buffer.BlockCopy(coverBytes, 0, payload, offset, coverBytes.Length);
            return BuildFrame("APIC", payload);
        }

        private static byte[] BuildFrame(string frameId, byte[] payload) {
            if (String.IsNullOrWhiteSpace(frameId) || frameId.Length != 4 || payload == null || payload.Length == 0) return null;

            byte[] frame = new byte[payload.Length + 10];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(frameId), 0, frame, 0, 4);
            WriteBigEndianInt(frame, 4, payload.Length);
            frame[8] = 0;
            frame[9] = 0;
            Buffer.BlockCopy(payload, 0, frame, 10, payload.Length);
            return frame;
        }

        private static void WriteFrame(Stream stream, byte[] frame) {
            if (stream == null || frame == null || frame.Length == 0) return;
            stream.Write(frame, 0, frame.Length);
        }

        private static void WriteBigEndianInt(byte[] buffer, int offset, int value) {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        private static byte[] EncodeSyncSafeSize(int value) {
            return new[] {
                (byte)((value >> 21) & 0x7F),
                (byte)((value >> 14) & 0x7F),
                (byte)((value >> 7) & 0x7F),
                (byte)(value & 0x7F)
            };
        }

        private static long TryGetExistingId3TagLength(FileStream stream) {
            Span<byte> header = stackalloc byte[10];
            stream.Position = 0;
            if (stream.Read(header) != header.Length) return 0;
            if (header[0] != 'I' || header[1] != 'D' || header[2] != '3') return 0;

            int size = (header[6] & 0x7F) << 21
                | (header[7] & 0x7F) << 14
                | (header[8] & 0x7F) << 7
                | (header[9] & 0x7F);
            return Math.Clamp(10L + size, 0, stream.Length);
        }

        private static async Task<bool> FileStartsWithId3Async(string path) {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

            byte[] header = new byte[3];
            await using FileStream stream = File.OpenRead(path);
            if (await stream.ReadAsync(header, 0, header.Length) != header.Length) return false;
            return header[0] == 'I' && header[1] == 'D' && header[2] == '3';
        }

        private static string ResolveCoverExtension(Uri uri) {
            string extension = Path.GetExtension(uri?.LocalPath ?? String.Empty)?.ToLowerInvariant();
            return extension is ".jpg" or ".jpeg" or ".png" or ".webp" ? extension : ".jpg";
        }

        private static string ResolveCoverMime(string path) {
            return Path.GetExtension(path)?.ToLowerInvariant() switch {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        private static void CleanupExportQaFiles(params string[] paths) {
            foreach (string path in paths) TryDeleteFile(path);
        }

        private static void TryDeleteFile(string path) {
            try {
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
            } catch {
            }
        }

        private static string BuildTrackSidecar(MusicTrackViewModel track, string filePath, string coverPath, bool id3Tagged) {
            return JsonSerializer.Serialize(new {
                artist = track.Artist,
                title = track.Title,
                duration = track.Audio.Duration,
                owner_id = track.Audio.OwnerId,
                id = track.Audio.Id,
                source = track.SourceTitle,
                vk_url = track.Link,
                file = filePath,
                cover_file = coverPath,
                id3_tagged = id3Tagged,
                downloaded_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string FormatUpdatedAt(long unix) {
            if (unix <= 0) return "давно";
            return DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("g");
        }
    }

    public sealed class MusicScrobbleEntry {
        public string Service { get; set; }
        public string Type { get; set; }
        public string Artist { get; set; }
        public string Title { get; set; }
        public string Album { get; set; }
        public long OwnerId { get; set; }
        public int Id { get; set; }
        public int Duration { get; set; }
        public string Link { get; set; }
        public long At { get; set; }
    }

    public sealed class MusicExportQaReport {
        public bool Passed { get; set; }
        public string Reason { get; set; }
        public string FilePath { get; set; }
        public long FileBytes { get; set; }
        public bool SidecarExists { get; set; }
        public bool Id3Header { get; set; }
        public bool Id3Tagged { get; set; }
        public bool CoverExists { get; set; }
        public long CoverBytes { get; set; }
        public bool ScrobbleExportExists { get; set; }
        public long ScrobbleExportBytes { get; set; }
        public int ScrobbleCount { get; set; }
    }
}
