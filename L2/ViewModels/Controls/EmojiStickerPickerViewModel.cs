using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Execute;
using ELOR.VKAPILib.Objects;
using Avalonia.Media;
using NeoSmart.Unicode;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.ViewModels.Controls {
    public sealed class EmojiPickerEmoji {
        private readonly string _packId;
        private readonly long _peerId;
        private readonly bool _canUseImage;
        private bool _imageUriResolved;
        private Uri _imageUri;

        public EmojiPickerEmoji(SingleEmoji emoji, string packId, long peerId) {
            Emoji = emoji;
            Text = emoji.ToString();
            _packId = packId;
            _peerId = peerId;
            _canUseImage = EmojiAssetResolver.IsImageBackedPack(packId);
            FontFamily = ELOR.Laney.Controls.MessageEmojiInlineRenderer.GetEmojiTextFontFamily(packId);
        }

        public SingleEmoji Emoji { get; }
        public string Text { get; }
        public Uri ImageUri => ResolveImageUri();
        public bool HasImage => ResolveImageUri() != null;
        public bool IsTextVisible => ImageUri == null;
        public FontFamily FontFamily { get; }

        private Uri ResolveImageUri() {
            if (!_canUseImage) return null;
            if (_imageUriResolved) return _imageUri;

            _imageUri = EmojiAssetResolver.ResolveImageUri(Text, _packId, _peerId);
            _imageUriResolved = true;
            return _imageUri;
        }
    }

    public sealed class EmojiPickerGroup : ObservableCollection<EmojiPickerEmoji> {
        public EmojiPickerGroup(EmojiGroup group, string packId, long peerId) : base(group.Select(e => new EmojiPickerEmoji(e, packId, peerId))) {
            Key = group.Key;
        }

        public string Key { get; }
    }

    public class EmojiStickerPickerViewModel : CommonViewModel {
        private ObservableCollection<TabItem<object>> _tabs = new ObservableCollection<TabItem<object>>();
        private TabItem<object> _selectedTab;
        private ObservableCollection<LocalSticker> _recentLocalStickers = new ObservableCollection<LocalSticker>();
        private ObservableCollection<LocalSticker> _filteredLocalStickers = new ObservableCollection<LocalSticker>();
        private string _localStickerQuery;

        public ObservableCollection<TabItem<object>> Tabs { get { return _tabs; } set { _tabs = value; OnPropertyChanged(); } }
        public TabItem<object> SelectedTab { get { return _selectedTab; } set { _selectedTab = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLocalStickerSearchVisible)); } }
        public ObservableCollection<LocalSticker> RecentLocalStickers { get { return _recentLocalStickers; } private set { _recentLocalStickers = value; OnPropertyChanged(); } }
        public ObservableCollection<LocalSticker> FilteredLocalStickers { get { return _filteredLocalStickers; } private set { _filteredLocalStickers = value; OnPropertyChanged(); } }
        public string LocalStickerQuery { get { return _localStickerQuery; } set { _localStickerQuery = value; OnPropertyChanged(); RefreshLocalStickers(); } }
        public bool IsLocalStickerSearchVisible => SelectedTab?.Content == FilteredLocalStickers;

        private VKSession session;

        public EmojiStickerPickerViewModel(VKSession session, long peerId = 0) {
            this.session = session;
            TabItem<object> emojiTab = new TabItem<object>(Assets.i18n.Resources.emoji, BuildEmojiGroups(peerId), VKIconNames.Icon20SmileOutline);
            Tabs.Add(emojiTab);
            AddLocalStickersTab();
            SelectedTab = Tabs.FirstOrDefault();

            new System.Action(async () => await LoadStickerPacksAsync())();
        }

        private static ObservableCollection<EmojiPickerGroup> BuildEmojiGroups(long peerId) {
            string packId = Settings.ResolvePeerEmojiPack(peerId);
            return new ObservableCollection<EmojiPickerGroup>(L2Emoji.GetForPeer(peerId).Select(g => new EmojiPickerGroup(g, packId, peerId)));
        }

        private void AddLocalStickersTab() {
            RefreshRecentLocalStickers();
            if (RecentLocalStickers.Count > 0) {
                Tabs.Add(new TabItem<object>("Недавние локальные", RecentLocalStickers, VKIconNames.Icon20RecentOutline));
            }

            RefreshLocalStickers();
            TabItem<object> localTab = new TabItem<object>("Локальные", FilteredLocalStickers, VKIconNames.Icon20ListBulletOutline);
            Tabs.Add(localTab);
        }

        public void ToggleLocalStickerFavorite(LocalSticker sticker) {
            if (sticker == null) return;

            LocalStickerStore.ToggleFavorite(sticker.Id);
            RefreshRecentLocalStickers();
            RefreshLocalStickers();
        }

        private void RefreshLocalStickers() {
            FilteredLocalStickers.Clear();
            foreach (LocalSticker sticker in LocalStickerStore.Search(LocalStickerQuery)) {
                FilteredLocalStickers.Add(sticker);
            }
        }

        private void RefreshRecentLocalStickers() {
            RecentLocalStickers.Clear();
            foreach (LocalSticker sticker in LocalStickerStore.GetRecent(32)) {
                RecentLocalStickers.Add(sticker);
            }
        }

        // TODO: кэш
        private async Task LoadStickerPacksAsync() {
            if (DemoMode.IsEnabled) return;
            try {
                var req1 = await session.API.GetRecentStickersAndGraffitiesAsync();
                TabItem<object> favTab = new TabItem<object>(Assets.i18n.Resources.favorites, new ObservableCollection<Sticker>(req1.FavoriteStickers), VKIconNames.Icon20FavoriteOutline);
                TabItem<object> recentTab = new TabItem<object>(Assets.i18n.Resources.recent, new ObservableCollection<Sticker>(req1.RecentStickers), VKIconNames.Icon20RecentOutline);
                Tabs.Add(favTab);
                Tabs.Add(recentTab);

                var req2 = await session.API.Store.GetProductsAsync("stickers", new List<string> { "active" }, true);
                foreach (var product in req2.Items) {
                    TabItem<object> spTab = new TabItem<object>(product.Title, new ObservableCollection<Sticker>(product.Stickers), image: product.Previews.FirstOrDefault().Uri);
                    Tabs.Add(spTab);
                }
                Log.Information($"EmojiStickerPickerVM: loaded {req2.Items.Count} sticker packs");
            } catch (Exception ex) {
                Log.Error(ex, "EmojiStickerPickerVM: Cannot get stickers!");
                // TODO: snackbar.
            }
        }
    }
}
