using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ELOR.Laney.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VKUI.Windows;

namespace ELOR.Laney.Views.Modals {
    public sealed partial class StoryViewerWindow : DialogWindow {
        private readonly VKSession session;
        private readonly List<Story> stories;
        private readonly Dictionary<Story, StoryRailItemViewModel> railItems;
        private int currentIndex;
        private Uri currentVideoUri;
        private Uri currentLinkUri;

        public StoryViewerWindow() {
            InitializeComponent();
            if (!Design.IsDesignMode) throw new ArgumentException();
        }

        private StoryViewerWindow(VKSession session, IReadOnlyList<Story> stories, int startIndex, Dictionary<Story, StoryRailItemViewModel> railItems = null) {
            InitializeComponent();
            this.FixDialogWindows(TitleBar, Root);

            this.session = session;
            this.stories = stories?.Where(s => s != null).ToList() ?? new List<Story>();
            this.railItems = railItems ?? new Dictionary<Story, StoryRailItemViewModel>();
            currentIndex = Math.Clamp(startIndex, 0, Math.Max(0, this.stories.Count - 1));
            Tag = session;
            DataContext = session;

            KeyDown += StoryViewerWindow_KeyDown;
            Opened += (a, b) => RenderCurrentStory();
        }

        public static async Task ShowAsync(Window owner, VKSession session, IReadOnlyList<StoryRailItemViewModel> items, StoryRailItemViewModel selectedItem = null) {
            if (session == null || items == null || items.Count == 0) return;

            List<StoryRailItemViewModel> availableItems = items.Where(i => i?.Story != null).ToList();
            if (availableItems.Count == 0) return;

            List<Story> stories = availableItems.Select(i => i.Story).ToList();
            int startIndex = Math.Max(0, availableItems.IndexOf(selectedItem));
            Dictionary<Story, StoryRailItemViewModel> map = availableItems
                .GroupBy(i => i.Story)
                .ToDictionary(g => g.Key, g => g.First());

            StoryViewerWindow window = new StoryViewerWindow(session, stories, startIndex, map);
            await window.ShowDialog(owner ?? session.ModalWindow);
        }

        public static async Task ShowAsync(Window owner, VKSession session, IReadOnlyList<Story> stories, Story selectedStory = null) {
            if (session == null || stories == null || stories.Count == 0) return;

            List<Story> storyList = stories.Where(s => s != null).ToList();
            if (storyList.Count == 0) return;

            int startIndex = selectedStory == null ? 0 : Math.Max(0, storyList.IndexOf(selectedStory));
            StoryViewerWindow window = new StoryViewerWindow(session, storyList, startIndex);
            await window.ShowDialog(owner ?? session.ModalWindow);
        }

        private void StoryViewerWindow_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case Key.Left:
                    GoToStory(currentIndex - 1);
                    e.Handled = true;
                    break;
                case Key.Right:
                    GoToStory(currentIndex + 1);
                    e.Handled = true;
                    break;
            }
        }

        private void PreviousButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            GoToStory(currentIndex - 1);
        }

        private void NextButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            GoToStory(currentIndex + 1);
        }

        private async void OpenVideoButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if (currentVideoUri == null) return;
            await ELOR.Laney.Core.Launcher.LaunchUrl(currentVideoUri);
        }

        private async void StoryLinkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if (currentLinkUri == null) return;
            await ELOR.Laney.Core.Launcher.LaunchUrl(currentLinkUri);
        }

        private void GoToStory(int index) {
            if (index < 0 || index >= stories.Count) return;
            currentIndex = index;
            RenderCurrentStory();
        }

        private void RenderCurrentStory() {
            if (stories.Count == 0) {
                Close();
                return;
            }

            Story story = stories[currentIndex];
            Uri mediaUri = GetStoryMediaUri(story);
            currentVideoUri = GetVideoUri(story?.Video);
            currentLinkUri = GetStoryLinkUri(story);

            OwnerName.Text = BuildOwnerTitle(story);
            StoryMeta.Text = BuildMeta(story);
            StoryCounter.Text = $"{currentIndex + 1}/{stories.Count}";
            OwnerAvatar.Initials = BuildInitials(OwnerName.Text);
            OwnerAvatar.Background = App.GetResource<IBrush>("VKAccentBrush");
            ImageLoader.SetImage(OwnerAvatar, GetOwnerAvatar(story));

            StoryLinkButton.IsVisible = currentLinkUri != null;
            StoryLinkText.Text = story?.Link?.Text ?? "Открыть ссылку";

            bool unavailable = story == null || story.IsDeleted || story.IsExpired || story.CanSee == 0;
            UnavailablePanel.IsVisible = unavailable || mediaUri == null;
            UnavailableTitle.Text = unavailable ? BuildUnavailableTitle(story) : "Не удалось загрузить кадр";
            UnavailableText.Text = unavailable ? BuildUnavailableText(story) : "VK не отдал подходящее изображение для этой истории.";

            ImageLoader.SetBackgroundSource(StoryMedia, unavailable ? null : mediaUri);
            VideoBadge.IsVisible = !unavailable && story?.Type == StoryType.Video;
            OpenVideoButton.IsVisible = !unavailable && story?.Type == StoryType.Video && currentVideoUri != null;

            bool canGoPrevious = currentIndex > 0;
            bool canGoNext = currentIndex < stories.Count - 1;
            PreviousButton.IsEnabled = canGoPrevious;
            PreviousOverlayButton.IsVisible = canGoPrevious;
            NextButton.IsEnabled = canGoNext;
            NextOverlayButton.IsVisible = canGoNext;

            MarkSeenLocally(story);
        }

        private void MarkSeenLocally(Story story) {
            if (Settings.ShouldSuppressStoryViewed || story == null) return;

            if (railItems.TryGetValue(story, out StoryRailItemViewModel item)) {
                item.MarkSeenLocally();
                return;
            }

            if (!story.IsDeleted && !story.IsExpired && story.CanSee != 0) story.Seen = 1;
        }

        private static Uri GetStoryMediaUri(Story story) {
            try {
                return story?.Type switch {
                    StoryType.Photo => GetBestPhotoUri(story.Photo),
                    StoryType.Video => GetBestVideoPosterUri(story.Video),
                    _ => null
                };
            } catch {
                return null;
            }
        }

        private static Uri GetBestPhotoUri(Photo photo) {
            PhotoSizes thumbnail = photo?.GetSizeAndUriForThumbnail(720, 1280);
            return photo?.MaximalSizedPhoto?.Uri ?? thumbnail?.Uri;
        }

        private static Uri GetBestVideoPosterUri(Video video) {
            Uri firstFrame = video?.FirstFrameForStory?.Uri;
            if (firstFrame != null) return firstFrame;

            return video?.Image?
                .Where(i => i?.Uri != null)
                .OrderBy(i => i.Width * i.Height)
                .LastOrDefault()
                ?.Uri;
        }

        private static Uri GetVideoUri(Video video) {
            if (video == null) return null;

            return TryCreateUri(video.Files?.MP4p1080)
                ?? TryCreateUri(video.Files?.MP4p720)
                ?? TryCreateUri(video.Files?.MP4p480)
                ?? TryCreateUri(video.Files?.MP4p360)
                ?? TryCreateUri(video.Files?.MP4p240)
                ?? TryCreateUri(video.Player);
        }

        private static Uri GetStoryLinkUri(Story story) {
            return TryCreateUri(story?.Link?.Url);
        }

        private static Uri TryCreateUri(string value) {
            if (String.IsNullOrWhiteSpace(value)) return null;
            return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ? uri : null;
        }

        private static Uri GetOwnerAvatar(Story story) {
            if (story == null || Settings.StreamerMode) return null;
            return CacheManager.GetNameAndAvatar(story.OwnerId)?.Item3;
        }

        private string BuildOwnerTitle(Story story) {
            if (story == null) return "История";
            if (Settings.StreamerMode) return "История";
            if (story.OwnerId == session?.Id) return "Твоя история";

            Tuple<string, string, Uri> owner = CacheManager.GetNameAndAvatar(story.OwnerId);
            if (owner != null) {
                string name = $"{owner.Item1} {owner.Item2}".Trim();
                if (!String.IsNullOrWhiteSpace(name)) return name;
            }

            return story.OwnerId < 0 ? $"Сообщество {-story.OwnerId}" : $"Пользователь {story.OwnerId}";
        }

        private static string BuildInitials(string title) {
            if (String.IsNullOrWhiteSpace(title)) return "И";
            return title.Trim()[0].ToString().ToUpperInvariant();
        }

        private static string BuildMeta(Story story) {
            if (story == null) return "story";

            string type = story.Type == StoryType.Video ? "видео" : "фото";
            string state = story.IsDeleted ? "удалена"
                : story.IsExpired ? "истекла"
                : story.CanSee == 0 ? "закрыта"
                : story.Seen == 1 ? "просмотрена"
                : "новая";
            return $"{type} · {state} · story{story.OwnerId}_{story.Id}";
        }

        private static string BuildUnavailableTitle(Story story) {
            if (story == null) return "История недоступна";
            if (story.IsDeleted) return "История удалена";
            if (story.IsExpired) return "История истекла";
            if (story.CanSee == 0) return "История закрыта";
            return "История недоступна";
        }

        private static string BuildUnavailableText(Story story) {
            if (story == null) return "VK не вернул данные по этой истории.";
            if (story.IsDeleted) return "Автор удалил историю, показывать уже нечего.";
            if (story.IsExpired) return "Срок жизни истории закончился.";
            if (story.CanSee == 0) return "Автор ограничил просмотр этой истории.";
            return "VK не отдал доступный media payload.";
        }
    }
}
