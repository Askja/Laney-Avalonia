using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class LocalStickerPackItemViewModel {
        public string Key { get; init; }
        public string Title { get; init; }
        public string Source { get; init; }
        public int Count { get; init; }
        public bool IsEnabled { get; set; }
        public string Subtitle => $"{Source} · {Count} шт.";
        public string State => IsEnabled ? "включён" : "скрыт";
    }

    public sealed class LocalStickerItemViewModel {
        public LocalSticker Sticker { get; init; }
        public string Title => Sticker?.Title;
        public string Kind => Sticker == null ? null : $"{Sticker.DisplayKind}{(Sticker.IsDisabled ? " · скрыт" : "")}";
        public string Tags { get; set; }
        public bool IsFavorite => Sticker?.IsFavorite == true;
    }

    public sealed class StickersViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> StickerAnimationModes { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(((int)StickerAnimationMode.Always).ToString(), "Всегда"),
            new TwoStringTuple(((int)StickerAnimationMode.Hover).ToString(), "При наведении"),
            new TwoStringTuple(((int)StickerAnimationMode.Click).ToString(), "По клику"),
            new TwoStringTuple(((int)StickerAnimationMode.Never).ToString(), "Никогда")
        };

        public ObservableCollection<TwoStringTuple> EmojiPackOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(EmojiPackIds.System, "Системные"),
            new TwoStringTuple(EmojiPackIds.TelegramLike, "Telegram/Noto Color"),
            new TwoStringTuple(EmojiPackIds.Twemoji, "Twemoji"),
            new TwoStringTuple(EmojiPackIds.Fallback, "Запасной набор"),
            new TwoStringTuple(EmojiPackIds.Custom, "Кастомный manifest")
        };

        public ObservableCollection<TwoStringTuple> LocalStickerSendModeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(LocalStickerSendModeIds.Auto, "Авто"),
            new TwoStringTuple(LocalStickerSendModeIds.Graffiti, "Граффити"),
            new TwoStringTuple(LocalStickerSendModeIds.Image, "Картинка"),
            new TwoStringTuple(LocalStickerSendModeIds.File, "Файл")
        };

        public ObservableCollection<LocalStickerPackItemViewModel> LocalStickerPacks { get; } = new ObservableCollection<LocalStickerPackItemViewModel>();
        public ObservableCollection<LocalStickerItemViewModel> LocalStickerItems { get; } = new ObservableCollection<LocalStickerItemViewModel>();

        public bool SuggestStickers { get { return Settings.SuggestStickers; } set { Settings.SuggestStickers = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public TwoStringTuple CurrentStickerAnimationMode { get { return GetStickerAnimationMode(); } set { ChangeStickerAnimationMode(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentEmojiPack { get { return GetEmojiPack(); } set { ChangeEmojiPack(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentLocalStickerSendMode { get { return GetLocalStickerSendMode(); } set { ChangeLocalStickerSendMode(value); OnPropertyChanged(); } }
        public string EmojiCustomPackPath { get { return Settings.EmojiCustomPackPath; } set { ChangeEmojiCustomPackPath(value); } }
        public string EmojiCustomPackPathSummary { get { return String.IsNullOrWhiteSpace(Settings.EmojiCustomPackPath) ? "Не выбран" : Settings.EmojiCustomPackPath; } }
        public string LocalStickerCountText { get { return $"{LocalStickerStore.GetAll().Count} локальных"; } }
        public string EmojiPackSummary => Settings.EmojiPack == EmojiPackIds.Custom && String.IsNullOrWhiteSpace(Settings.EmojiCustomPackPath) ? "кастомный пак без файла, будет запасной набор" : GetEmojiPack().Item2;
        public string LocalStickerManagerSummary => $"{LocalStickerPacks.Count} паков · {LocalStickerItems.Count} стикеров в списке";
        private string _localStickerManagerQuery;
        public string LocalStickerManagerQuery { get { return _localStickerManagerQuery; } set { _localStickerManagerQuery = value; OnPropertyChanged(); ReloadLocalManager(); } }

        public StickersViewModel() {
            ReloadLocalManager();
        }

        private TwoStringTuple GetStickerAnimationMode() {
            string id = ((int)Settings.StickerAnimation).ToString();
            return StickerAnimationModes.Where(m => m.Item1 == id).FirstOrDefault() ?? StickerAnimationModes[1];
        }

        private void ChangeStickerAnimationMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.StickerAnimation = (StickerAnimationMode)int.Parse(value.Item1);
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentStickerAnimationMode));
        }

        private TwoStringTuple GetLocalStickerSendMode() {
            string id = Settings.LocalStickerSendMode;
            return LocalStickerSendModeOptions.Where(m => m.Item1 == id).FirstOrDefault() ?? LocalStickerSendModeOptions[0];
        }

        private void ChangeLocalStickerSendMode(TwoStringTuple value) {
            if (value == null) return;

            Settings.LocalStickerSendMode = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentLocalStickerSendMode));
        }

        private TwoStringTuple GetEmojiPack() {
            string id = Settings.EmojiPack;
            return EmojiPackOptions.Where(m => m.Item1 == id).FirstOrDefault() ?? EmojiPackOptions[0];
        }

        private void ChangeEmojiPack(TwoStringTuple value) {
            if (value == null) return;

            Settings.EmojiPack = value.Item1;
            L2Emoji.ClearCache();
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentEmojiPack));
            OnPropertyChanged(nameof(EmojiPackSummary));
        }

        private void ChangeEmojiCustomPackPath(string value) {
            string normalized = value?.Trim() ?? String.Empty;
            if (Settings.EmojiCustomPackPath == normalized) return;

            Settings.EmojiCustomPackPath = normalized;
            L2Emoji.ClearCache();
            MarkCustomProfile();
            OnPropertyChanged(nameof(EmojiCustomPackPath));
            OnPropertyChanged(nameof(EmojiCustomPackPathSummary));
            OnPropertyChanged(nameof(EmojiPackSummary));
        }

        public void ReloadLocalManager() {
            LocalStickerPacks.Clear();
            foreach (LocalStickerPackInfo pack in LocalStickerStore.GetPacks()) {
                LocalStickerPacks.Add(new LocalStickerPackItemViewModel {
                    Key = pack.Key,
                    Title = pack.Title,
                    Source = pack.Source,
                    Count = pack.Count,
                    IsEnabled = pack.IsEnabled
                });
            }

            LocalStickerItems.Clear();
            foreach (LocalSticker sticker in LocalStickerStore.GetAll(true).Where(MatchesLocalStickerQuery).Take(80)) {
                LocalStickerItems.Add(new LocalStickerItemViewModel {
                    Sticker = sticker,
                    Tags = sticker.Tags
                });
            }

            OnPropertyChanged(nameof(LocalStickerCountText));
            OnPropertyChanged(nameof(LocalStickerManagerSummary));
        }

        public void SetPackEnabled(LocalStickerPackItemViewModel pack, bool isEnabled) {
            if (pack == null) return;

            LocalStickerStore.SetPackEnabled(pack.Key, isEnabled);
            ReloadLocalManager();
        }

        public void MovePack(LocalStickerPackItemViewModel pack, int delta) {
            if (pack == null) return;

            LocalStickerStore.MovePack(pack.Key, delta);
            ReloadLocalManager();
        }

        public void ToggleFavorite(LocalStickerItemViewModel item) {
            if (item?.Sticker == null) return;

            LocalStickerStore.ToggleFavorite(item.Sticker.Id);
            ReloadLocalManager();
        }

        public void SaveTags(LocalStickerItemViewModel item) {
            if (item?.Sticker == null) return;

            LocalStickerStore.SetStickerTags(item.Sticker.Id, item.Tags);
            ReloadLocalManager();
        }

        private bool MatchesLocalStickerQuery(LocalSticker sticker) {
            if (String.IsNullOrWhiteSpace(LocalStickerManagerQuery)) return true;

            string query = LocalStickerManagerQuery.Trim().ToLowerInvariant();
            string haystack = $"{sticker.Title} {sticker.Tags} {sticker.Extension} {sticker.Source} {sticker.SourcePack}".ToLowerInvariant();
            return haystack.Contains(query);
        }

        private void MarkCustomProfile() {
            if (Settings.InterfaceProfile != InterfaceProfileIds.Custom) Settings.InterfaceProfile = InterfaceProfileIds.Custom;
        }
    }
}
