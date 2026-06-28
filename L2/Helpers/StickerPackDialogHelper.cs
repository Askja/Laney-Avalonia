using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ELOR.Laney.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Helpers {
    public static class StickerPackDialogHelper {
        public static async Task ShowAsync(VKSession session, string packName) {
            if (session == null || String.IsNullOrWhiteSpace(packName)) return;

            StoreProduct product = await LoadStickerPackAsync(session, packName);
            if (product == null || product.Stickers == null || product.Stickers.Count == 0) {
                await ShowFallbackAsync(session, packName);
                return;
            }

            string url = GetStickerPackUrl(packName);
            VKUIDialog dialog = new VKUIDialog(product.Title ?? packName, null, [Assets.i18n.Resources.close, Assets.i18n.Resources.open], 2) {
                DialogContent = BuildContent(product, packName)
            };

            if (await dialog.ShowDialog<int>(session.ModalWindow) == 2) {
                await Launcher.LaunchUrl(url);
            }
        }

        private static async Task<StoreProduct> LoadStickerPackAsync(VKSession session, string packName) {
            try {
                StoreProduct product = await new VKUIWaitDialog<StoreProduct>().ShowAsync(session.ModalWindow,
                    session.API.Store.GetStockItemByNameAsync(packName));
                if (IsValidPack(product)) return product;
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to load sticker pack {PackName} by stock item name. Trying products fallback.", packName);
            }

            try {
                StoreProductsList products = await new VKUIWaitDialog<StoreProductsList>().ShowAsync(session.ModalWindow,
                    session.API.Store.GetProductsAsync("stickers", new List<string> { "active", "promoted" }, true));
                return products?.Items?.FirstOrDefault(p => IsSamePack(p, packName));
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to load sticker products fallback for {PackName}.", packName);
                return null;
            }
        }

        private static Control BuildContent(StoreProduct product, string packName) {
            StackPanel root = new StackPanel {
                Spacing = 12,
                MinWidth = 360
            };

            TextBlock meta = new TextBlock {
                Text = GetSubtitle(product, packName),
                TextWrapping = TextWrapping.Wrap
            };
            meta.Classes.Add("Caption1");
            root.Children.Add(meta);

            if (!String.IsNullOrWhiteSpace(product.Description)) {
                TextBlock description = new TextBlock {
                    Text = product.Description,
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 4
                };
                root.Children.Add(description);
            }

            WrapPanel stickers = new WrapPanel {
                Width = 360,
                ItemWidth = 72,
                ItemHeight = 72
            };

            foreach (Sticker sticker in product.Stickers.Take(80)) {
                Image image = new Image {
                    Width = 64,
                    Height = 64,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                StickerImage stickerImage = sticker.GetSizeAndUriForThumbnail(96);
                if (stickerImage != null) ImageLoader.SetSource(image, stickerImage.Uri);

                stickers.Children.Add(new Border {
                    Width = 72,
                    Height = 72,
                    Child = image
                });
            }

            root.Children.Add(new ScrollViewer {
                MaxHeight = 360,
                Content = stickers
            });

            if (product.Stickers.Count > 80) {
                TextBlock tail = new TextBlock {
                    Text = $"Показаны первые 80 из {product.Stickers.Count}. Остальное пусть не жрет память просто потому что может.",
                    TextWrapping = TextWrapping.Wrap
                };
                tail.Classes.Add("Caption1");
                root.Children.Add(tail);
            }

            return root;
        }

        private static async Task ShowFallbackAsync(VKSession session, string packName) {
            VKUIDialog dialog = new VKUIDialog(
                "Стикеры",
                $"Не удалось получить превью пака `{packName}` через VK API. Можно открыть магазин, но Laney не будет притворяться, что увидел то, чего API не отдал.",
                [Assets.i18n.Resources.close, Assets.i18n.Resources.open],
                2
            );

            if (await dialog.ShowDialog<int>(session.ModalWindow) == 2) {
                await Launcher.LaunchUrl(GetStickerPackUrl(packName));
            }
        }

        private static bool IsValidPack(StoreProduct product) {
            return product != null && ((product.Stickers != null && product.Stickers.Count > 0) || !String.IsNullOrEmpty(product.Title));
        }

        private static bool IsSamePack(StoreProduct product, string packName) {
            if (product == null) return false;
            return String.Equals(product.Name, packName, StringComparison.OrdinalIgnoreCase)
                || String.Equals(product.Title, packName, StringComparison.OrdinalIgnoreCase)
                || product.Id.ToString() == packName;
        }

        private static string GetSubtitle(StoreProduct product, string packName) {
            List<string> parts = new List<string>();
            if (!String.IsNullOrWhiteSpace(product.Name)) parts.Add(product.Name);
            if (product.Price != null && !String.IsNullOrWhiteSpace(product.Price.Text)) parts.Add(product.Price.Text);
            parts.Add(product.IsPurchased || product.IsActive ? "уже доступен" : "можно посмотреть перед покупкой");
            parts.Add($"{product.Stickers?.Count ?? 0} стикеров");
            return String.Join(" · ", parts);
        }

        private static string GetStickerPackUrl(string packName) {
            return $"https://vk.com/stickers/{Uri.EscapeDataString(packName)}";
        }
    }
}
