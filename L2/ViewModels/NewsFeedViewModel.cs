using Avalonia.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.ViewModels {
    public sealed class NewsFeedPostViewModel {
        public NewsFeedPostViewModel(VKSession session, WallPost post, bool isAdLike, string adReason) {
            Session = session;
            Post = post;
            IsAdLike = isAdLike;
            AdReason = adReason;
        }

        public VKSession Session { get; }
        public WallPost Post { get; }
        public bool IsAdLike { get; }
        public bool IsAdBadgeVisible => IsAdLike;
        public string AdReason { get; }
        public string Link => Post == null ? "https://vk.com/feed" : $"https://vk.com/wall{Post.OwnerOrToId}_{Post.Id}";
    }

    public sealed class NewsFeedViewModel : CommonViewModel {
        private const int PageSize = 25;
        private const string FilterAll = "all";
        private const string FilterPhoto = "photo";
        private const string FilterVideo = "video";
        private const string FilterText = "text";

        private readonly VKSession session;
        private TwoStringTuple _currentFilter;
        private bool _hideAds = true;
        private bool _initialized;
        private bool _hasMore = true;
        private string _nextFrom;
        private int _blockedAdsCount;

        public NewsFeedViewModel(VKSession session) {
            this.session = session;
            _currentFilter = FilterOptions[0];
        }

        public ObservableCollection<NewsFeedPostViewModel> Posts { get; } = new ObservableCollection<NewsFeedPostViewModel>();
        public ObservableCollection<TwoStringTuple> FilterOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(FilterAll, "Все посты"),
            new TwoStringTuple(FilterPhoto, "С фото"),
            new TwoStringTuple(FilterVideo, "С видео"),
            new TwoStringTuple(FilterText, "Только текст")
        };

        public TwoStringTuple CurrentFilter {
            get { return _currentFilter; }
            set {
                if (value == null || value == _currentFilter) return;
                _currentFilter = value;
                OnPropertyChanged();
                RefreshAfterUserChange();
            }
        }

        public bool HideAds {
            get { return _hideAds; }
            set {
                if (_hideAds == value) return;
                _hideAds = value;
                OnPropertyChanged();
                RefreshAfterUserChange();
            }
        }

        public int BlockedAdsCount { get { return _blockedAdsCount; } private set { _blockedAdsCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(BlockedAdsText)); OnPropertyChanged(nameof(HasBlockedAds)); } }
        public bool HasBlockedAds => BlockedAdsCount > 0;
        public string BlockedAdsText => BlockedAdsCount == 0 ? null : $"скрыто промо: {BlockedAdsCount}";
        public bool HasPosts => Posts.Count > 0;
        public bool IsLoadingFirstPage => IsLoading && Posts.Count == 0;
        public bool CanLoadMore => _hasMore && !IsLoading;

        public async Task InitializeAsync() {
            if (_initialized) return;
            _initialized = true;
            await RefreshAsync();
        }

        public async Task RefreshAsync() {
            Posts.Clear();
            _nextFrom = null;
            _hasMore = true;
            BlockedAdsCount = 0;
            Placeholder = null;
            RefreshPostState();
            await LoadNextAsync();
        }

        public async Task LoadNextAsync() {
            if (IsLoading || !_hasMore) return;

            SetLoading(true);
            try {
                if (DemoMode.IsEnabled) {
                    LoadDemoPosts();
                    return;
                }

                await LoadApiPageAsync();
            } catch (Exception ex) {
                Placeholder = PlaceholderViewModel.GetForException(ex, _ => new System.Action(async () => await RefreshAsync())());
            } finally {
                SetLoading(false);
                UpdateEmptyState();
            }
        }

        private void RefreshAfterUserChange() {
            if (!_initialized) return;
            new System.Action(async () => await RefreshAsync())();
        }

        private async Task LoadApiPageAsync() {
            Dictionary<string, string> parameters = new Dictionary<string, string> {
                { "filters", "post" },
                { "count", PageSize.ToString() },
                { "max_photos", "6" },
                { "return_banned", "0" },
                { "fields", "photo_50,photo_100,photo_200,screen_name" }
            };
            if (!String.IsNullOrWhiteSpace(_nextFrom)) parameters["start_from"] = _nextFrom;

            using JsonDocument document = await session.API.CallMethodAsync("newsfeed.get", parameters);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("error", out JsonElement error)) {
                string message = error.TryGetProperty("error_msg", out JsonElement errorMessage)
                    ? errorMessage.GetString()
                    : "VK API вернул ошибку без текста.";
                throw new InvalidOperationException(message);
            }

            if (!root.TryGetProperty("response", out JsonElement response)) {
                _hasMore = false;
                return;
            }

            CacheNewsOwners(response);
            _nextFrom = response.TryGetProperty("next_from", out JsonElement nextFrom) ? nextFrom.GetString() : null;
            _hasMore = !String.IsNullOrWhiteSpace(_nextFrom);

            if (!response.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array) return;

            foreach (JsonElement item in items.EnumerateArray()) {
                WallPost post = TryParsePost(item);
                if (post == null || !MatchesCurrentFilter(post)) continue;

                bool isAd = IsAdLike(item, post, out string reason);
                if (HideAds && isAd) {
                    BlockedAdsCount++;
                    continue;
                }

                Posts.Add(new NewsFeedPostViewModel(session, post, isAd, reason));
            }

            RefreshPostState();
        }

        private void LoadDemoPosts() {
            IEnumerable<ChatViewModel> chats = session?.ImViewModel?.SortedChats ?? Enumerable.Empty<ChatViewModel>();
            int index = 0;
            foreach (ChatViewModel chat in chats.Take(18)) {
                string preview = chat.LastMessage?.DisplayPreviewText;
                WallPost post = new WallPost {
                    OwnerId = chat.PeerId,
                    FromId = chat.PeerId,
                    ToId = chat.PeerId,
                    Id = ++index,
                    DateUnix = (int)DateTimeOffset.Now.AddMinutes(-index * 17).ToUnixTimeSeconds(),
                    Text = String.IsNullOrWhiteSpace(preview)
                        ? $"{chat.DisplayTitle}\nНовость из demo-ленты Laney."
                        : $"{chat.DisplayTitle}\n{preview}",
                    Attachments = new List<Attachment>()
                };

                if (!MatchesCurrentFilter(post)) continue;
                Posts.Add(new NewsFeedPostViewModel(session, post, false, null));
            }

            _hasMore = false;
            RefreshPostState();
        }

        private bool MatchesCurrentFilter(WallPost post) {
            string filter = CurrentFilter?.Item1 ?? FilterAll;
            bool hasAttachments = post.Attachments?.Count > 0;
            return filter switch {
                FilterPhoto => post.Attachments?.Any(a => a.Type == AttachmentType.Photo) == true,
                FilterVideo => post.Attachments?.Any(a => a.Type == AttachmentType.Video) == true,
                FilterText => !String.IsNullOrWhiteSpace(post.Text) && !hasAttachments,
                _ => true
            };
        }

        private static WallPost TryParsePost(JsonElement item) {
            if (item.TryGetProperty("type", out JsonElement typeElement)
                && !String.Equals(typeElement.GetString(), "post", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            JsonObject node = JsonNode.Parse(item.GetRawText()) as JsonObject;
            if (node == null) return null;

            if (!node.ContainsKey("owner_id") && item.TryGetProperty("source_id", out JsonElement sourceId)) node["owner_id"] = sourceId.GetInt64();
            if (!node.ContainsKey("from_id") && item.TryGetProperty("source_id", out sourceId)) node["from_id"] = sourceId.GetInt64();
            if (!node.ContainsKey("to_id") && item.TryGetProperty("source_id", out sourceId)) node["to_id"] = sourceId.GetInt64();
            if (!node.ContainsKey("id") && item.TryGetProperty("post_id", out JsonElement postId)) node["id"] = postId.GetInt32();

            return (WallPost)JsonSerializer.Deserialize(node.ToJsonString(), typeof(WallPost), BuildInJsonContext.Default);
        }

        private static bool IsAdLike(JsonElement item, WallPost post, out string reason) {
            if (post?.MarkedAsAds == 1 || HasIntFlag(item, "marked_as_ads")) {
                reason = "помечено VK";
                return true;
            }

            if (item.TryGetProperty("type", out JsonElement typeElement)) {
                string type = typeElement.GetString();
                if (ContainsAny(type, "ads", "ad", "promo", "promoted")) {
                    reason = "promo";
                    return true;
                }
            }

            string text = post?.Text;
            if (ContainsAny(text, "erid:", "erid ", "erid=", "#реклама", "реклама.", "sponsored", "adfox")) {
                reason = "рекламная метка";
                return true;
            }

            reason = null;
            return false;
        }

        private static bool HasIntFlag(JsonElement item, string propertyName) {
            return item.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out int intValue)
                && intValue != 0;
        }

        private static bool ContainsAny(string text, params string[] patterns) {
            if (String.IsNullOrWhiteSpace(text)) return false;
            return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        private static void CacheNewsOwners(JsonElement response) {
            if (response.TryGetProperty("profiles", out JsonElement profiles) && profiles.ValueKind == JsonValueKind.Array) {
                List<User> users = (List<User>)JsonSerializer.Deserialize(profiles.GetRawText(), typeof(List<User>), BuildInJsonContext.Default);
                CacheManager.Add(users);
            }

            if (response.TryGetProperty("groups", out JsonElement groups) && groups.ValueKind == JsonValueKind.Array) {
                List<Group> communities = (List<Group>)JsonSerializer.Deserialize(groups.GetRawText(), typeof(List<Group>), BuildInJsonContext.Default);
                CacheManager.Add(communities);
            }
        }

        private void UpdateEmptyState() {
            if (Posts.Count > 0 || Placeholder != null) return;

            Placeholder = new PlaceholderViewModel {
                Icon = new VKIcon { Id = VKIconNames.Icon56InfoOutline },
                Header = "Лента пуста",
                Text = BlockedAdsCount > 0 ? "После фильтров и скрытия рекламы ничего не осталось." : "VK не вернул посты для выбранного фильтра.",
                ActionButton = "Обновить",
                ActionButtonFunc = new RelayCommand(_ => new System.Action(async () => await RefreshAsync())())
            };
        }

        private void SetLoading(bool value) {
            IsLoading = value;
            OnPropertyChanged(nameof(IsLoadingFirstPage));
            OnPropertyChanged(nameof(CanLoadMore));
        }

        private void RefreshPostState() {
            OnPropertyChanged(nameof(HasPosts));
            OnPropertyChanged(nameof(IsLoadingFirstPage));
            OnPropertyChanged(nameof(CanLoadMore));
        }
    }
}
