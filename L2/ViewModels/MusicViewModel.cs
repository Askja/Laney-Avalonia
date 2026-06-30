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

    public sealed class MusicViewModel : CommonViewModel {
        private const int PageSize = 100;

        private readonly VKSession session;
        private bool _initialized;
        private bool _hasMore = true;
        private int _offset;
        private string _query = String.Empty;
        private TwoStringTuple _currentSource;
        private TwoStringTuple _currentAudioDspMode;
        private string _statusText = "Готово";
        private string _downloadStatusText = "Скачанные треки сохраняются локально с sidecar JSON.";
        private AudioPlayerViewModel _currentPlayer;

        public MusicViewModel(VKSession session) {
            this.session = session;
            _currentSource = SourceOptions[0];
            _currentAudioDspMode = AudioDspModes.FirstOrDefault(m => m.Item1 == Settings.AudioDspMode) ?? AudioDspModes[0];
            CurrentPlayer = AudioPlayerViewModel.MainInstance;

            AudioPlayerViewModel.InstancesChanged += AudioPlayerViewModel_InstancesChanged;
            EnsureEqualizerPreviewBands();
            RefreshEqualizerPreview();
        }

        public ObservableCollection<MusicTrackViewModel> Tracks { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<MusicTrackViewModel> VisibleTracks { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<MusicTrackViewModel> Queue { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<MusicTrackViewModel> History { get; } = new ObservableCollection<MusicTrackViewModel>();
        public ObservableCollection<AudioEqualizerBandViewModel> EqualizerPreviewBands { get; } = new ObservableCollection<AudioEqualizerBandViewModel>();

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
            new TwoStringTuple(AudioDspModeIds.BassBoost, "Больше баса")
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
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        public string DownloadStatusText { get => _downloadStatusText; private set { _downloadStatusText = value; OnPropertyChanged(); } }
        public string QueueSummary => Queue.Count == 0 ? "Очередь пуста" : $"В очереди: {Queue.Count}";
        public string LibrarySummary => $"{VisibleTracks.Count} из {GetCurrentSourceTracks().Count()} · {(_hasMore ? "можно грузить еще" : "конец списка")}";
        public string IntegrationStatusText => "VK status работает сразу; Discord/AIMP/Last.fm складываем в локальную очередь scrobble.";
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

        public async Task DownloadTrackAsync(MusicTrackViewModel track) {
            if (track?.Audio?.Uri == null) return;

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

                await File.WriteAllTextAsync(target + ".json", BuildTrackSidecar(track));
                DownloadStatusText = $"Скачано: {Path.GetFileName(target)}";
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

            string directory = Path.Combine(App.LocalDataPath, "integrations");
            Directory.CreateDirectory(directory);
            string line = JsonSerializer.Serialize(new {
                service = "local",
                type = "music.scrobble",
                artist = track.Artist,
                title = track.Title,
                owner_id = track.Audio.OwnerId,
                id = track.Audio.Id,
                at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await File.AppendAllTextAsync(Path.Combine(directory, "music-scrobbles.jsonl"), line + Environment.NewLine);
            StatusText = "Скроббл добавлен в локальную очередь.";
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
            if (Tracks.Count == 0) {
                string samplePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "bb2.mp3");
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

            foreach (string label in new[] { "60", "125", "250", "1k", "3k", "6k", "12k" }) {
                EqualizerPreviewBands.Add(new AudioEqualizerBandViewModel(label));
            }
        }

        private void RefreshEqualizerPreview() {
            float[] frequencies = [60, 125, 250, 1000, 3000, 6000, 12000];
            for (int i = 0; i < EqualizerPreviewBands.Count && i < frequencies.Length; i++) {
                EqualizerPreviewBands[i].Update(LMediaPlayer.GetEqualizerPreviewAmp(Settings.AudioDspMode, frequencies[i]));
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

        private static string BuildTrackSidecar(MusicTrackViewModel track) {
            return JsonSerializer.Serialize(new {
                artist = track.Artist,
                title = track.Title,
                duration = track.Audio.Duration,
                owner_id = track.Audio.OwnerId,
                id = track.Audio.Id,
                source = track.SourceTitle,
                vk_url = track.Link,
                downloaded_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string FormatUpdatedAt(long unix) {
            if (unix <= 0) return "давно";
            return DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("g");
        }
    }
}
