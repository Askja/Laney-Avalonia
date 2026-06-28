using ELOR.Laney.Core;
using ELOR.Laney.Core.Network;
using ELOR.Laney.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class MemoryViewModel : CommonViewModel {
        private string _lastUpdatedText;
        private string _processBudgetText;
        private string _workingSetText;
        private string _managedHeapBudgetText;
        private string _gcBudgetText;
        private string _mediaBudgetText;
        private string _mediaBudgetPressureText;
        private string _waveformBudgetText;
        private string _localAnimationBudgetText;
        private string _prefetchBudgetText;
        private string _bitmapCacheBudgetText;
        private string _bitmapCacheBreakdownText;
        private string _bitmapCachePressureText;
        private string _networkBudgetText;
        private string _networkBudgetBadge;
        private string _historyIndexStateText;
        private string _historyIndexProgressText;
        private string _historyIndexCountersText;
        private string _historyIndexPolicyText;
        private string _historyIndexBadge;
        private string _decodeAnimationBudgetText;
        private string _modeBudgetText;
        private RelayCommand _refreshPerformanceBudgetCommand;
        private RelayCommand _clearBitmapCacheCommand;

        public bool LowMotionMode { get { return Settings.LowMotionMode; } set { Settings.LowMotionMode = value; RefreshModeDependentProperties(); OnPropertyChanged(); } }
        public bool LowMemoryMode { get { return Settings.LowMemoryMode; } set { Settings.LowMemoryMode = value; RefreshModeDependentProperties(); OnPropertyChanged(); } }
        public bool LoadImagesSequential { get { return Settings.LoadImagesSequential; } set { Settings.LoadImagesSequential = value; RefreshPerformanceBudget(); OnPropertyChanged(); } }
        public bool ShowRAMUsage { get { return Settings.ShowRAMUsage; } set { Settings.ShowRAMUsage = value; OnPropertyChanged(); } }
        public string MediaMemoryBudgetMbText { get { return Settings.MediaMemoryBudgetMb.ToString(); } set { SetInt(value, 64, 1024, v => Settings.MediaMemoryBudgetMb = v, nameof(MediaMemoryBudgetMbText)); } }
        public string ImageCacheRamLimitMbText { get { return Settings.ImageCacheRamLimitMb.ToString(); } set { SetInt(value, 16, 512, v => Settings.ImageCacheRamLimitMb = v, nameof(ImageCacheRamLimitMbText)); } }
        public string ImageCacheDefaultTtlMinutesText { get { return Settings.ImageCacheDefaultTtlMinutes.ToString(); } set { SetInt(value, 0, Int32.MaxValue, v => Settings.ImageCacheDefaultTtlMinutes = v, nameof(ImageCacheDefaultTtlMinutesText)); } }
        public string ImageCacheAvatarTtlMinutesText { get { return Settings.ImageCacheAvatarTtlMinutes.ToString(); } set { SetInt(value, 0, Int32.MaxValue, v => Settings.ImageCacheAvatarTtlMinutes = v, nameof(ImageCacheAvatarTtlMinutesText)); } }
        public string ImageCacheAttachmentTtlMinutesText { get { return Settings.ImageCacheAttachmentTtlMinutes.ToString(); } set { SetInt(value, 0, Int32.MaxValue, v => Settings.ImageCacheAttachmentTtlMinutes = v, nameof(ImageCacheAttachmentTtlMinutesText)); } }
        public string ImageCacheE2ETtlMinutesText { get { return Settings.ImageCacheE2ETtlMinutes.ToString(); } set { SetInt(value, 0, Int32.MaxValue, v => Settings.ImageCacheE2ETtlMinutes = v, nameof(ImageCacheE2ETtlMinutesText)); } }
        public string LastUpdatedText { get { return _lastUpdatedText; } private set { _lastUpdatedText = value; OnPropertyChanged(); } }
        public string ProcessBudgetText { get { return _processBudgetText; } private set { _processBudgetText = value; OnPropertyChanged(); } }
        public string WorkingSetText { get { return _workingSetText; } private set { _workingSetText = value; OnPropertyChanged(); } }
        public string ManagedHeapBudgetText { get { return _managedHeapBudgetText; } private set { _managedHeapBudgetText = value; OnPropertyChanged(); } }
        public string GcBudgetText { get { return _gcBudgetText; } private set { _gcBudgetText = value; OnPropertyChanged(); } }
        public string MediaBudgetText { get { return _mediaBudgetText; } private set { _mediaBudgetText = value; OnPropertyChanged(); } }
        public string MediaBudgetPressureText { get { return _mediaBudgetPressureText; } private set { _mediaBudgetPressureText = value; OnPropertyChanged(); } }
        public string WaveformBudgetText { get { return _waveformBudgetText; } private set { _waveformBudgetText = value; OnPropertyChanged(); } }
        public string LocalAnimationBudgetText { get { return _localAnimationBudgetText; } private set { _localAnimationBudgetText = value; OnPropertyChanged(); } }
        public string PrefetchBudgetText { get { return _prefetchBudgetText; } private set { _prefetchBudgetText = value; OnPropertyChanged(); } }
        public string BitmapCacheBudgetText { get { return _bitmapCacheBudgetText; } private set { _bitmapCacheBudgetText = value; OnPropertyChanged(); } }
        public string BitmapCacheBreakdownText { get { return _bitmapCacheBreakdownText; } private set { _bitmapCacheBreakdownText = value; OnPropertyChanged(); } }
        public string BitmapCachePressureText { get { return _bitmapCachePressureText; } private set { _bitmapCachePressureText = value; OnPropertyChanged(); } }
        public string NetworkBudgetText { get { return _networkBudgetText; } private set { _networkBudgetText = value; OnPropertyChanged(); } }
        public string NetworkBudgetBadge { get { return _networkBudgetBadge; } private set { _networkBudgetBadge = value; OnPropertyChanged(); } }
        public string HistoryIndexStateText { get { return _historyIndexStateText; } private set { _historyIndexStateText = value; OnPropertyChanged(); } }
        public string HistoryIndexProgressText { get { return _historyIndexProgressText; } private set { _historyIndexProgressText = value; OnPropertyChanged(); } }
        public string HistoryIndexCountersText { get { return _historyIndexCountersText; } private set { _historyIndexCountersText = value; OnPropertyChanged(); } }
        public string HistoryIndexPolicyText { get { return _historyIndexPolicyText; } private set { _historyIndexPolicyText = value; OnPropertyChanged(); } }
        public string HistoryIndexBadge { get { return _historyIndexBadge; } private set { _historyIndexBadge = value; OnPropertyChanged(); } }
        public string DecodeAnimationBudgetText { get { return _decodeAnimationBudgetText; } private set { _decodeAnimationBudgetText = value; OnPropertyChanged(); } }
        public string ModeBudgetText { get { return _modeBudgetText; } private set { _modeBudgetText = value; OnPropertyChanged(); } }
        public RelayCommand RefreshPerformanceBudgetCommand { get { return _refreshPerformanceBudgetCommand; } private set { _refreshPerformanceBudgetCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearBitmapCacheCommand { get { return _clearBitmapCacheCommand; } private set { _clearBitmapCacheCommand = value; OnPropertyChanged(); } }

        public MemoryViewModel() {
            RefreshPerformanceBudgetCommand = new RelayCommand((o) => RefreshPerformanceBudget());
            ClearBitmapCacheCommand = new RelayCommand((o) => ClearBitmapCache());
            RefreshPerformanceBudget();
        }

        private void SetInt(string value, int min, int max, Action<int> setter, string propertyName) {
            if (!Int32.TryParse(value, out int parsed)) return;
            setter(Math.Clamp(parsed, min, max));
            RefreshPerformanceBudget();
            OnPropertyChanged(propertyName);
        }

        private void RefreshModeDependentProperties() {
            OnPropertyChanged(nameof(LoadImagesSequential));
            OnPropertyChanged(nameof(ShowRAMUsage));
            OnPropertyChanged(nameof(MediaMemoryBudgetMbText));
            OnPropertyChanged(nameof(ImageCacheRamLimitMbText));
            OnPropertyChanged(nameof(ImageCacheDefaultTtlMinutesText));
            OnPropertyChanged(nameof(ImageCacheAvatarTtlMinutesText));
            OnPropertyChanged(nameof(ImageCacheAttachmentTtlMinutesText));
            OnPropertyChanged(nameof(ImageCacheE2ETtlMinutesText));
            RefreshPerformanceBudget();
        }

        private void RefreshPerformanceBudget() {
            Process process = Process.GetCurrentProcess();
            process.Refresh();

            BitmapCacheSnapshot cacheSnapshot = BitmapManager.GetCacheSnapshot();
            MediaMemorySnapshot mediaSnapshot = MediaMemoryGovernor.GetSnapshot();
            LNetQueueSnapshot networkSnapshot = LNet.GetQueueSnapshot();
            HistoryStatisticsSnapshot historySnapshot = BackgroundHistoryStatisticsIndexer.GetSnapshot();
            long managedHeap = GC.GetTotalMemory(false);
            long totalAllocated = GC.GetTotalAllocatedBytes(false);
            double cachePressure = cacheSnapshot.LimitBytes <= 0 ? 0 : (double)cacheSnapshot.SizeBytes / cacheSnapshot.LimitBytes;
            double mediaPressure = mediaSnapshot.TotalBudgetBytes <= 0 ? 0 : (double)mediaSnapshot.EstimatedUsedBytes / mediaSnapshot.TotalBudgetBytes;

            LastUpdatedText = $"Обновлено {DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
            ProcessBudgetText = $"private {FormatBytes(process.PrivateMemorySize64)} · peak WS {FormatBytes(process.PeakWorkingSet64)}";
            WorkingSetText = FormatBytes(process.WorkingSet64);
            ManagedHeapBudgetText = $"heap {FormatBytes(managedHeap)} · total allocated {FormatBytes(totalAllocated)}";
            GcBudgetText = $"GC gen0/1/2: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}";
            MediaBudgetText = $"{FormatBytes(mediaSnapshot.EstimatedUsedBytes)} / {FormatBytes(mediaSnapshot.TotalBudgetBytes)} · headroom {FormatBytes(mediaSnapshot.HeadroomBytes)}";
            MediaBudgetPressureText = FormatPercent(mediaPressure);
            WaveformBudgetText = $"{mediaSnapshot.WaveformCacheItems}/{MediaMemoryGovernor.GetWaveformCacheItemLimit()} · {FormatBytes(mediaSnapshot.WaveformCacheBytes)}";
            LocalAnimationBudgetText = $"{mediaSnapshot.ActiveLocalStickerAnimations}/{mediaSnapshot.LocalStickerAnimationLimit} active · {(MediaMemoryGovernor.CanStartLocalStickerAnimation() ? "can start" : "gated")}";
            PrefetchBudgetText = FeatureFlags.IsEnabled(FeatureFlags.PreloadNextChat) ? (mediaSnapshot.CanPrefetchMedia ? "prefetch allowed" : "prefetch gated by headroom") : "prefetch feature off";
            BitmapCacheBudgetText = $"{FormatBytes(cacheSnapshot.SizeBytes)} / {FormatBytes(cacheSnapshot.LimitBytes)} · элементов {cacheSnapshot.EntryCount} · грузится {cacheSnapshot.LoadingCount}";
            BitmapCacheBreakdownText = $"default {cacheSnapshot.DefaultCount} · avatars {cacheSnapshot.AvatarCount} · attachments {cacheSnapshot.AttachmentCount} · e2e {cacheSnapshot.E2ECount}";
            BitmapCachePressureText = FormatPercent(cachePressure);
            NetworkBudgetText = $"active {networkSnapshot.ActiveRequests} · sequential GET {networkSnapshot.SequentialGetRequests} · POST {networkSnapshot.SequentialPostRequests} · VK Queue {(VKQueue.IsInitialized ? "on" : "off")}";
            NetworkBudgetBadge = networkSnapshot.ActiveRequests > 0 ? $"{networkSnapshot.ActiveRequests} active" : "idle";
            HistoryIndexStateText = BuildHistoryIndexStateText(historySnapshot);
            HistoryIndexProgressText = BuildHistoryIndexProgressText(historySnapshot);
            HistoryIndexCountersText = $"text {FormatCompact(historySnapshot.TextMessages)} · service {FormatCompact(historySnapshot.ServiceMessages)} · attachments {FormatCompact(historySnapshot.Attachments)} · reactions {FormatCompact(historySnapshot.Reactions)}";
            HistoryIndexPolicyText = $"page {historySnapshot.PageSize} · delay {historySnapshot.ApiDelayMs} ms · api calls {FormatCompact(historySnapshot.ApiCalls)} · errors {historySnapshot.ErrorCount}";
            HistoryIndexBadge = historySnapshot.IsPaused ? "battery pause" : historySnapshot.IsRunning ? "indexing" : $"{historySnapshot.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture)}%";
            DecodeAnimationBudgetText = $"{(Settings.LoadImagesSequential ? "decode sequential" : "decode parallel")} · stickers {GetStickerAnimationText(Settings.StickerAnimation)} · low traffic {(Settings.LowTrafficMode ? "on" : "off")}";
            ModeBudgetText = $"{(Settings.LowMemoryMode ? "low RAM" : "normal RAM")} · {(Settings.LowMotionMode ? "low motion" : "motion on")}";
        }

        private void ClearBitmapCache() {
            BitmapManager.ClearCachedImages();
            RefreshPerformanceBudget();
        }

        private static string GetStickerAnimationText(StickerAnimationMode mode) {
            return mode switch {
                StickerAnimationMode.Always => "always",
                StickerAnimationMode.Hover => "hover",
                StickerAnimationMode.Click => "click",
                StickerAnimationMode.Never => "off",
                _ => "unknown"
            };
        }

        private static string FormatBytes(long bytes) {
            double mb = bytes / 1048576d;
            if (mb < 1024) return $"{mb.ToString("0.##", CultureInfo.InvariantCulture)} MB";

            double gb = mb / 1024d;
            return $"{gb.ToString("0.##", CultureInfo.InvariantCulture)} GB";
        }

        private static string FormatPercent(double value) {
            double normalized = Math.Clamp(value, 0, 9.99) * 100;
            return $"{normalized.ToString("0.#", CultureInfo.InvariantCulture)}%";
        }

        private static string BuildHistoryIndexStateText(HistoryStatisticsSnapshot snapshot) {
            string state = snapshot.State switch {
                "running" => "идет индексация",
                "paused_battery" => "пауза на батарее",
                "completed" => "индекс готов",
                "error" => "ошибка индексации",
                _ => "ожидает фонового прохода"
            };

            string peer = !String.IsNullOrWhiteSpace(snapshot.CurrentPeerTitle)
                ? $" · сейчас: {snapshot.CurrentPeerTitle}"
                : String.Empty;
            string error = !String.IsNullOrWhiteSpace(snapshot.LastError)
                ? $" · last error: {snapshot.LastError}"
                : String.Empty;

            return $"{state}{peer}{error}";
        }

        private static string BuildHistoryIndexProgressText(HistoryStatisticsSnapshot snapshot) {
            return $"{snapshot.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture)}% · peers {snapshot.IndexedPeers}/{snapshot.TotalPeers} · messages {FormatCompact(snapshot.MessagesScanned)}/{FormatCompact(snapshot.TotalMessagesEstimate)}";
        }

        private static string FormatCompact(long value) {
            if (value >= 1_000_000) return $"{(value / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture)}M";
            if (value >= 10_000) return $"{(value / 1_000d).ToString("0.#", CultureInfo.InvariantCulture)}K";
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
