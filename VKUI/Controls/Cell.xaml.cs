using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using System;

namespace VKUI.Controls {
    public class Cell : TemplatedControl {
        #region Template controls

        bool isTemplateLoaded = false;
        ContentPresenter SemanticIconControl;
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
            SemanticIconControl = e.NameScope.Find<ContentPresenter>(nameof(SemanticIconControl));
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
            if (SemanticIconControl == null || BeforeControl == null) return;

            bool canShowSemanticIcon = AutoBeforeIcon && !String.IsNullOrWhiteSpace(Header) && ShouldShowSemanticIconFor(Before);
            SemanticIconControl.Content = canShowSemanticIcon ? CreateSemanticIcon(Header) : null;
            SemanticIconControl.IsVisible = canShowSemanticIcon;

            if (Before != null) {
                BeforeControl.Content = Before;
                BeforeControl.IsVisible = true;
                return;
            }

            BeforeControl.Content = null;
            BeforeControl.IsVisible = false;
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

        public const string DefaultSemanticIconId = VKIconNames.Icon20GearOutline;

        public static string GetSemanticIconIdForHeader(string header) {
            if (String.IsNullOrWhiteSpace(header)) return DefaultSemanticIconId;

            string text = header.ToLowerInvariant();

            if (ContainsAny(text, "поиск", "search")) return VKIconNames.Icon28SearchOutline;
            if (ContainsAny(text, "профиль", "аккаунт", "учет", "учёт", "account", "profile")) return VKIconNames.Icon20UserOutline;
            if (ContainsAny(text, "язык", "language")) return VKIconNames.Icon20EducationOutline;
            if (ContainsAny(text, "panic-замок", "panic lock", "горячая клавиша panic")) return VKIconNames.Icon20LockOutline;
            if (ContainsAny(text, "стример", "privacy", "приват", "невидим", "прочитан", "online", "stories", "story", "shadow", "shadow-ban", "набор текста", "panic", "blacklist", "заблок", "кто видит", "видят", "доступ", "не отмечать", "не показывать набор", "не слать", "буфер", "clipboard", "скрыть")) return VKIconNames.Icon28PrivacyOutline;
            if (ContainsAny(text, "упомин", "mention")) return VKIconNames.Icon20MentionOutline;
            if (ContainsAny(text, "автозапуск", "startup", "start", "запуск", "свернут", "свёрнут", "minimized")) return VKIconNames.Icon20DoorEnterArrowRightOutline;
            if (ContainsAny(text, "пресет", "тема", "theme", "палитра", "palette", "акцент", "accent", "оформ", "цвет", "color", "прозрач", "transparent", "яркость", "brightness", "затемн", "darken")) return VKIconNames.Icon28PaletteOutline;
            if (ContainsAny(text, "enter отправляет", "отправк", "отправляет", "send")) return VKIconNames.Icon28Send;
            if (ContainsAny(text, "шрифт", "font", "формат", "format", "текст", "text", "подпись")) return VKIconNames.Icon24TextLiveOutline;
            if (ContainsAny(text, "иконк", "icon", "картин", "picture", "image", "images", "фон", "background", "изображ", "галере", "gallery", "ocr", "tesseract", "фото", "photo", "пикчер", "blur", "размыт")) return VKIconNames.Icon28PictureOutline;
            if (ContainsAny(text, "аватар", "avatar")) return VKIconNames.Icon20UserOutline;
            if (ContainsAny(text, "галоч")) return VKIconNames.Icon16CheckDoubleOutline;
            if (ContainsAny(text, "чат", "chat", "сообщ", "message", "пузыр", "bubble", "диалог", "dialog")) return VKIconNames.Icon24MessagesOutline;
            if (ContainsAny(text, "строк", "список", "list", "макет", "ширина", "width", "размер списка", "форма", "плотность", "density", "layout", "stack size")) return VKIconNames.Icon20ListBulletOutline;
            if (ContainsAny(text, "уведом", "notification", "delivery", "звук", "sound", "toast", "тихие часы", "тишин", "позиция уведом", "screen position", "таймаут", "timeout", "стек", "stack")) return VKIconNames.Icon20NotificationOutline;
            if (ContainsAny(text, "стикер", "sticker", "emoji", "эмод", "смайл", "пак", "pack", "telegram")) return VKIconNames.Icon24SmileOutline;
            if (ContainsAny(text, "повтор", "loop")) return VKIconNames.Icon24RepeatOutline;
            if (ContainsAny(text, "аудио", "музык", "трек", "плеер", "подкаст", "громк", "перемот", "dsp", "эквал", "скорость музыки", "скорость подкастов", "volume", "seek")) return VKIconNames.Icon28MusicOutline;
            if (ContainsAny(text, "голос", "voice", "whisper", "расшифров", "модель whisper", "скорость голосовых", "waveform")) return VKIconNames.Icon28VoiceOutline;
            if (ContainsAny(text, "продолжать с места", "позиция", "история прослушивания")) return VKIconNames.Icon20RecentOutline;
            if (ContainsAny(text, "влож", "файл", "документ", "json", "backup", "папк", "экспорт", "импорт", "скачан", "хранилище", "vault", "модель", "путь", "директор")) return VKIconNames.Icon28DocumentOutline;
            if (ContainsAny(text, "синхро", "распис", "период", "после простоя", "активности пк", "ttl", "с места", "очеред")) return VKIconNames.Icon20RecentOutline;
            if (ContainsAny(text, "сеть", "network", "health-check", "probe", "api", "proxy", "прокси", "long poll", "lnet", "трафик", "буфер api", "адрес", "локальные адреса", "фоновых групп", "домен", "domain", "версия api", "api version")) return VKIconNames.Icon28LinkCircleOutline;
            if (ContainsAny(text, "e2e", "x25519", "xchacha", "chacha", "poly1305", "aes", "hmac", "sha256", "sha-256", "handshake", "fingerprint", "sas", "paranoid", "legacy-compatible", "lock", "блокир", "ключ", "шифр", "сверк", "вериф", "verified")) return VKIconNames.Icon20LockOutline;
            if (ContainsAny(text, "истори", "history")) return VKIconNames.Icon20StoryOutline;
            if (ContainsAny(text, "статист", "график", "метрик")) return VKIconNames.Icon20PollOutline;
            if (ContainsAny(text, "счетчик", "счётчик")) return VKIconNames.Icon20PollOutline;
            if (ContainsAny(text, "быстрые", "fast", "действ", "action", "actions", "команд", "command", "шаблон", "template", "автоответ", "авто", "правил", "rules", "состояние", "state", "статус", "status", "активност", "activity", "ключевые", "keywords", "группа", "кто", "где", "когда", "vip", "important", "critical", "управление")) return VKIconNames.Icon20ServicesOutline;
            if (ContainsAny(text, "памят", "ram", "кэш", "cache", "куча", "процесс", "медиа", "prefetch", "лимит", "анимац", "последователь", "бюджет", "декод", "экономия памяти", "меньше анимаций")) return VKIconNames.Icon28SettingsOutline;
            if (ContainsAny(text, "debug", "лог", "fps", "render", "рендер", "bitmapmanager", "extra", "dev-", "инструмент", "консоль", "diagnostic")) return VKIconNames.Icon28BugOutline;
            if (ContainsAny(text, "важн", "vip")) return VKIconNames.Icon20FavoriteOutline;
            if (ContainsAny(text, "назад")) return VKIconNames.Icon28ArrowLeftOutline;
            if (ContainsAny(text, "фокус", "поле ввода", "ввод")) return VKIconNames.Icon20WriteOutline;
            if (ContainsAny(text, "шаблоны группы", "бот", "клавиатур")) return VKIconNames.Icon28KeyboardBotsOutline;
            if (ContainsAny(text, "ссыл", "парсить ссылки")) return VKIconNames.Icon24LinkedOutline;
            if (ContainsAny(text, "очист", "удал")) return VKIconNames.Icon20DeleteOutline;
            if (ContainsAny(text, "режим", "по умолчанию", "обычный", "активный режим", "настройк")) return VKIconNames.Icon28SettingsOutline;

            return DefaultSemanticIconId;
        }

        private static bool ContainsAny(string text, params string[] values) {
            foreach (string value in values) {
                if (text.Contains(value, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public static bool ShouldShowSemanticIconFor(Control before) {
            if (before == null) return true;
            if (before is RadioButton || before is CheckBox) return true;

            return false;
        }

        private static VKIcon CreateSemanticIcon(string header) {
            return new VKIcon {
                Width = 24,
                Height = 24,
                Id = GetSemanticIconIdForHeader(header)
            };
        }

        #endregion
    }
}
