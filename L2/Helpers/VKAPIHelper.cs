using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Helpers {
    public static class VKAPIHelper {
        public static readonly List<string> Fields = new List<string>(26) {
            "photo_200", "photo_100", "photo_50",
            "ban_info", "blacklisted", "blacklisted_by_me", "can_message", "can_write_private_message", "friend_status",
            "is_messages_blocked", "online_info", "domain", "verified", "sex", "activity",
            "first_name_gen", "first_name_dat", "first_name_acc", "first_name_ins", "first_name_abl",
            "last_name_gen", "last_name_dat", "last_name_acc", "last_name_ins", "last_name_abl", "photo_avg_color"
        };

        public static readonly List<string> UserFields = new List<string>(38) {
            "has_photo", "photo_avg_color", "verified", "sex",
            "bdate", "city", "country", "occupation", "has_photo", "photo_50", "photo_100", "photo_200",
            "online_info", "domain", "has_mobile", "contacts", "site", "universities", "schools", "status", "followers_count", "activities",
            "can_write_private_message", "can_send_friend_request", "is_favorite", "timezone", "screen_name", "maiden_name", "is_friend", "friend_status",
            "career", "blacklisted", "blacklisted_by_me", "first_name_gen", "first_name_acc", "last_name_gen", "last_name_acc", "owner_state"
        };

        public static readonly List<string> GroupFields = new List<string>(20) {
            "has_photo", "photo_avg_color", "name_history",
            "city", "country", "can_message", "place", "description", "members_count", "activity",
            "status", "verified", "site", "photo_200", "photo_100", "photo_50", "ban_info", "can_message", "is_messages_blocked", "domain"
        };

        #region Errors

        public static string GetUnderstandableErrorMessage(int code) {
            string key = $"api_error_{code}";
            string value = Assets.i18n.Resources.ResourceManager.GetString(key, Assets.i18n.Resources.Culture);
            return !string.IsNullOrEmpty(value) ? value : string.Empty;
        }

        public static string GetUnderstandableErrorMessage(APIException ex) {
            string uem = GetUnderstandableErrorMessage(ex.Code);
            return string.IsNullOrEmpty(uem) ? $"{ex.Message} ({ex.Code})" : uem;
        }

        public static string GetUnderstandableErrorMessage(int code, string message) {
            string uem = GetUnderstandableErrorMessage(code);
            return string.IsNullOrEmpty(uem) ? $"{message} ({code})" : uem;
        }

        #endregion

        public static string GetOnlineInfo(UserOnlineInfo info, Sex sex) {
            if (info != null) {
                if (info.Visible) {
                    if (info.IsOnline) {
                        return Assets.i18n.Resources.online;
                    } else {
                        return info.LastSeen.Year >= 2006 ?
                            Localizer.GetFormatted(sex, "offline_last_seen", info.LastSeen.ToHumanizedString()) :
                            Assets.i18n.Resources.offline; // у забаненных/удалённых возвращается 0 в unixtime. 
                    }
                } else {
                    switch (info.Status) {
                        case UserOnlineStatus.Recently: return Localizer.Get("offline_recently", sex);
                        case UserOnlineStatus.LastWeek: return Localizer.Get("offline_last_week", sex);
                        case UserOnlineStatus.LastMonth: return Localizer.Get("offline_last_month", sex);
                        case UserOnlineStatus.LongAgo: return Localizer.Get("offline_long_ago", sex);
                    }
                }
            }
            return Assets.i18n.Resources.offline;
        }

        public static string GetSenderNameShort(MessageViewModel msg) {
            if (msg.Action != null) return string.Empty;
            StringBuilder sender = new StringBuilder();

            if (msg.SenderId == VKSession.Main.UserId && msg.PeerId != VKSession.Main.UserId) {
                sender.Append(Assets.i18n.Resources.you);
            } else if (msg.PeerId.IsChat()) {
                sender.Append(CacheManager.GetNameOnly(msg.SenderId, true));
            }

            if (sender.Length > 0) sender.Append(": ");
            return sender.ToString();
        }

        public static SolidColorBrush GetDocumentIconBackground(DocumentType type) {
            switch (type) {
                case DocumentType.Text: return new SolidColorBrush(Color.FromArgb(255, 0, 122, 204));
                case DocumentType.Archive: return new SolidColorBrush(Color.FromArgb(255, 118, 185, 121));
                case DocumentType.GIF: return new SolidColorBrush(Color.FromArgb(255, 119, 165, 214));
                case DocumentType.Image: return new SolidColorBrush(Color.FromArgb(255, 119, 165, 214));
                case DocumentType.Audio: return new SolidColorBrush(Color.FromArgb(255, 186, 104, 200));
                case DocumentType.Video: return new SolidColorBrush(Color.FromArgb(255, 229, 115, 155));
                case DocumentType.EBook: return new SolidColorBrush(Color.FromArgb(255, 255, 174, 56));
                default: return new SolidColorBrush(Color.FromArgb(255, 119, 165, 214));
            }
        }

        public static string GetDocumentIcon(DocumentType type) {
            switch (type) {
                case DocumentType.Text: return VKIconNames.Icon28ArticleOutline;
                case DocumentType.Archive: return VKIconNames.Icon28ZipOutline;
                case DocumentType.GIF: return VKIconNames.Icon28PictureOutline;
                case DocumentType.Image: return VKIconNames.Icon28PictureOutline;
                case DocumentType.Audio: return VKIconNames.Icon28MusicOutline;
                case DocumentType.Video: return VKIconNames.Icon28VideoOutline;
                case DocumentType.EBook: return VKIconNames.Icon28ArticleOutline;
                default: return VKIconNames.Icon28DocumentOutline;
            }
        }

        // User https://vk.com/id1756935 have non-standart birthdate, so do NOT convert to DateTime!
        public static string GetNormalizedBirthDate(string bdate) {
            string[] a = bdate.Split('.');
            var formatInfo = new DateTimeFormatInfo();
            var monthName = formatInfo.GetMonthName(Int32.Parse(a[1]));
            return a.Length == 3 ? $"{a[0]} {monthName} {a[2]}" : $"{a[0]} {monthName}";
        }

        public static string GetNameOrDefaultString(long ownerId, string defaultStr = null) {
            if (!String.IsNullOrEmpty(defaultStr)) return defaultStr;
            string from = "";
            if (ownerId.IsUser()) {
                User u = CacheManager.GetUser(ownerId);
                from = u != null ? $"{Assets.i18n.Resources.from} {u.FirstNameGen} {u.LastNameGen}" : "";
            } else if (ownerId.IsGroup()) {
                Group u = CacheManager.GetGroup(ownerId);
                from = u != null ? $"{Assets.i18n.Resources.from} \"{u.Name}\"" : "";
            }
            return from;
        }

        // TODO: убрать методы кнопок ботов в отдельный класс.

        internal static void GenerateButtons(StackPanel root, List<List<BotButton>> buttons, Func<BotButton, Task> clickHandler = null) {
            bool isFirstRow = true;
            foreach (var row in CollectionsMarshal.AsSpan(buttons)) {
                Grid buttonsRow = new Grid {
                    ColumnDefinitions = new ColumnDefinitions(),
                    Margin = new Thickness(0, isFirstRow ? 0 : 6, 0, 0)
                };
                for (byte i = 0; i < row.Count; i++) {
                    buttonsRow.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Star });
                    BotButton botButton = row[i];
                    Button button = BuildButton(botButton);
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.Click += async (a, b) => await HandleButtonClickAsync(root, botButton, clickHandler, b);

                    Grid.SetColumn(button, i);
                    buttonsRow.Children.Add(button);
                }
                isFirstRow = false;
                root.Children.Add(buttonsRow);
            }
        }

        internal static void GenerateButtons(StackPanel root, List<BotButton> buttons, Func<BotButton, Task> clickHandler = null) {
            bool isFirstRow = true;
            foreach (BotButton botButton in CollectionsMarshal.AsSpan(buttons)) {
                Button button = BuildButton(botButton);
                button.Margin = new Thickness(button.Margin.Left, isFirstRow ? 0 : 8, button.Margin.Right, button.Margin.Bottom);
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                button.Click += async (a, b) => await HandleButtonClickAsync(root, botButton, clickHandler, b);

                isFirstRow = false;
                root.Children.Add(button);
            }
        }

        private static async Task HandleButtonClickAsync(StackPanel root, BotButton button, Func<BotButton, Task> clickHandler, RoutedEventArgs e) {
            e.Handled = true;

            if (clickHandler != null) {
                await clickHandler(button);
                return;
            }

            ExceptionHelper.ShowNotImplementedDialog(VKSession.GetByDataContext(root).ModalWindow);
        }

        private static Button BuildButton(BotButton button) {
            Button buttonUI = new Button {
                Margin = new Thickness(3, 0),
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(12),
                MinHeight = 34,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            buttonUI.Classes.Add("Medium");

            Grid content = new Grid {
                MinHeight = 34,
                ColumnDefinitions = new ColumnDefinitions("20,*,20")
            };

            VKIcon icon = new VKIcon {
                Width = 18,
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsVisible = false,
            };

            TextBlock label = new TextBlock {
                FontSize = 13,
                LineHeight = 17,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeight.Medium,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                Margin = new Thickness(2, 4)
            };
            if (button.Color != BotButtonColor.Default || button.Action.Type == BotButtonType.VKPay)
                label.Classes.Add("ButtonIn");

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(label, 1);
            content.Children.Add(icon);
            content.Children.Add(label);
            buttonUI.Content = content;

            if (button.Action.Type == BotButtonType.VKPay) {
                buttonUI.Classes.Add("Primary");
            } else {
                switch (button.Color) {
                    case BotButtonColor.Primary:
                        buttonUI.Classes.Add("Primary");
                        icon.RegisterThemeResource(VKIcon.ForegroundProperty, "VKButtonPrimaryForegroundBrush");
                        break;
                    case BotButtonColor.Positive:
                        buttonUI.Classes.Add("Commerce");
                        icon.RegisterThemeResource(VKIcon.ForegroundProperty, "VKButtonCommerceForegroundBrush");
                        break;
                    case BotButtonColor.Negative:
                        buttonUI.Classes.Add("Negative");
                        icon.RegisterThemeResource(VKIcon.ForegroundProperty, "VKButtonCommerceForegroundBrush");
                        break;
                    default:
                        icon.RegisterThemeResource(VKIcon.ForegroundProperty, "VKButtonSecondaryForegroundBrush");
                        break;
                }
            }

            switch (button.Action.Type) {
                case BotButtonType.VKPay:
                    label.Text = "Pay via VK Pay";
                    break;
                case BotButtonType.Location:
                    label.Text = Assets.i18n.Resources.geo;
                    icon.Id = VKIconNames.Icon20PlaceOutline;
                    icon.IsVisible = true;
                    break;
                case BotButtonType.OpenApp:
                    label.Text = button.Action.Label;
                    icon.Id = VKIconNames.Icon20ServicesOutline;
                    icon.IsVisible = true;
                    break;
                default:
                    label.Text = button.Action.Label;
                    break;
            }

            if (button.Action.Type == BotButtonType.OpenLink) {
                var arrowIcon = new VKIcon {
                    Width = 14,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Id = VKIconNames.Icon12ArrowUpRight
                };
                arrowIcon.RegisterThemeResource(VKIcon.ForegroundProperty, "VKButtonSecondaryForegroundBrush");
                Grid.SetColumn(arrowIcon, 2);
                content.Children.Add(arrowIcon);
            }
            return buttonUI;
        }
    }
}
