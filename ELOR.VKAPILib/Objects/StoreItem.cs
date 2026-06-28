using System.Text.Json.Serialization;

namespace ELOR.VKAPILib.Objects {
    public class StoreProductPrice {
        public StoreProductPrice() { }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }

    public class StoreProduct {
        public StoreProduct() { }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("is_purchased")]
        public bool IsPurchased { get; set; }

        [JsonPropertyName("price")]
        public StoreProductPrice Price { get; set; }

        [JsonPropertyName("stickers")]
        public List<Sticker> Stickers { get; set; }

        [JsonPropertyName("previews")]
        public List<StickerImage> Previews { get; set; }
    }
}
