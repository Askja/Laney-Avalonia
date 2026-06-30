using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using System;

namespace VKUI.Controls {
    public class Cell : TemplatedControl {
        #region Template controls

        bool isTemplateLoaded = false;
        ContentPresenter BeforeControl;
        ContentPresenter AfterControl;

        #endregion

        #region Properties

        public static readonly StyledProperty<Control> BeforeProperty =
            AvaloniaProperty.Register<Cell, Control>(nameof(Before));

        public static readonly StyledProperty<string> HeaderProperty =
            AvaloniaProperty.Register<Cell, string>(nameof(Header));

        public static readonly StyledProperty<string> SubtitleProperty =
            AvaloniaProperty.Register<Cell, string>(nameof(Subtitle));

        public static readonly StyledProperty<object> AfterProperty =
            AvaloniaProperty.Register<Cell, object>(nameof(After));

        public static readonly StyledProperty<bool> AutoBeforeIconProperty =
            AvaloniaProperty.Register<Cell, bool>(nameof(AutoBeforeIcon), true);

        public Control Before {
            get => GetValue(BeforeProperty);
            set => SetValue(BeforeProperty, value);
        }

        public string Header {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string Subtitle {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public object After {
            get => GetValue(AfterProperty);
            set => SetValue(AfterProperty, value);
        }

        public bool AutoBeforeIcon {
            get => GetValue(AutoBeforeIconProperty);
            set => SetValue(AutoBeforeIconProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);
            BeforeControl = e.NameScope.Find<ContentPresenter>(nameof(BeforeControl));
            AfterControl = e.NameScope.Find<ContentPresenter>(nameof(AfterControl));

            isTemplateLoaded = true;
            CheckBeforeValue();
            CheckAfterValue();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (!isTemplateLoaded) return;

            if (change.Property == BeforeProperty || change.Property == HeaderProperty || change.Property == AutoBeforeIconProperty) CheckBeforeValue();
            if (change.Property == AfterProperty) CheckAfterValue();
        }

        private void CheckBeforeValue() {
            if (BeforeControl == null) return;

            if (Before != null) {
                BeforeControl.Content = Before;
                BeforeControl.IsVisible = true;
                return;
            }

            if (!AutoBeforeIcon || String.IsNullOrWhiteSpace(Header)) {
                BeforeControl.Content = null;
                BeforeControl.IsVisible = false;
                return;
            }

            BeforeControl.Content = new VKIcon {
                Width = 24,
                Height = 24,
                Id = GetSemanticIconId(Header)
            };
            BeforeControl.IsVisible = true;
        }

        private void CheckAfterValue() {
            if (AfterControl == null) return;

            if (After == null) {
                AfterControl.Content = null;
            } else if (After is Control control) {
                AfterControl.Content = control;
            } else if (After is string text) {
                AfterControl.Content = new TextBlock {
                    Text = text
                };
            } else {
                throw new ArgumentException("Wrong value type! Required Conrol or string", nameof(After));
            }
        }

        private static string GetSemanticIconId(string header) {
            string text = header.ToLowerInvariant();

            if (ContainsAny(text, "поиск", "search")) return VKIconNames.Icon28SearchOutline;
            if (ContainsAny(text, "профиль", "аккаунт", "учет", "учёт")) return VKIconNames.Icon20UserOutline;
            if (ContainsAny(text, "язык", "language")) return VKIconNames.Icon20EducationOutline;
            if (ContainsAny(text, "автозапуск", "startup", "start")) return VKIconNames.Icon20DoorEnterArrowRightOutline;
            if (ContainsAny(text, "пресет", "тема", "палитра", "акцент", "оформ", "цвет")) return VKIconNames.Icon28PaletteOutline;
            if (ContainsAny(text, "шрифт", "формат", "текст")) return VKIconNames.Icon24TextLiveOutline;
            if (ContainsAny(text, "иконк", "картин", "фон", "изображ", "галере", "ocr", "tesseract")) return VKIconNames.Icon28PictureOutline;
            if (ContainsAny(text, "аватар")) return VKIconNames.Icon20UserOutline;
            if (ContainsAny(text, "чат", "сообщ", "пузыр", "галоч", "строк", "список", "макет", "ширина")) return VKIconNames.Icon24MessagesOutline;
            if (ContainsAny(text, "уведом", "звук", "sound", "toast")) return VKIconNames.Icon20NotificationOutline;
            if (ContainsAny(text, "стикер", "emoji", "эмод", "смайл")) return VKIconNames.Icon24SmileOutline;
            if (ContainsAny(text, "аудио", "музык", "трек", "плеер", "подкаст", "громк", "перемот", "dsp", "эквал", "whisper")) return VKIconNames.Icon28MusicOutline;
            if (ContainsAny(text, "голос")) return VKIconNames.Icon28VoiceOutline;
            if (ContainsAny(text, "влож", "файл", "документ", "json", "backup", "папк", "экспорт", "импорт")) return VKIconNames.Icon28DocumentOutline;
            if (ContainsAny(text, "синхро")) return VKIconNames.Icon20RecentOutline;
            if (ContainsAny(text, "сеть", "api", "proxy", "прокси", "long poll", "lnet", "трафик", "буфер api")) return VKIconNames.Icon28LinkCircleOutline;
            if (ContainsAny(text, "стример", "privacy", "приват", "невидим", "прочитан", "online", "stories", "panic", "буфер", "blacklist", "заблок")) return VKIconNames.Icon28PrivacyOutline;
            if (ContainsAny(text, "lock", "блокир", "ключ")) return VKIconNames.Icon20LockOutline;
            if (ContainsAny(text, "авто", "правил", "распис", "период", "тихие", "статус", "активност", "ключевые")) return VKIconNames.Icon20ServicesOutline;
            if (ContainsAny(text, "памят", "ram", "кэш", "cache", "куча", "процесс", "медиа", "waveform", "prefetch", "лимит", "анимац", "последователь", "бюджет", "ttl", "декод")) return VKIconNames.Icon28SettingsOutline;
            if (ContainsAny(text, "debug", "лог", "fps", "render", "рендер", "bitmapmanager", "extra")) return VKIconNames.Icon28BugOutline;
            if (ContainsAny(text, "важн", "vip", "упомин")) return VKIconNames.Icon20FavoriteOutline;
            if (ContainsAny(text, "назад")) return VKIconNames.Icon28ArrowLeftOutline;
            if (ContainsAny(text, "фокус")) return VKIconNames.Icon20WriteOutline;

            return VKIconNames.Icon20GearOutline;
        }

        private static bool ContainsAny(string text, params string[] values) {
            foreach (string value in values) {
                if (text.Contains(value, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        #endregion
    }
}
