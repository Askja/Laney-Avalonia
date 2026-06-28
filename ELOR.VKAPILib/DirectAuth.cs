using ELOR.VKAPILib.Objects.Auth;
using System.Text.Json;

namespace ELOR.VKAPILib {
    public class DirectAuth {
        public static async Task<VKAPI> GetVKAPIWithAnonymTokenAsync(int clientId, string clientSecret, string userAgent, Func<Uri, Dictionary<string, string>, Dictionary<string, string>, Task<HttpResponseMessage>> webRequestCallback = null) {
            Dictionary<string, string> p = new Dictionary<string, string> {
                { "client_id", clientId.ToString() },
                { "client_secret", clientSecret }
            };

            VKAPI api = new VKAPI(null, "en", userAgent);
            api.WebRequestCallback = webRequestCallback;

            using var response = await api.SendRequestAsync(new Uri("https://oauth.vk.ru/get_anonym_token"), p);
            using var respStream = await response.ReadAsStreamAsync();

            AnonymToken atr = (AnonymToken)await JsonSerializer.DeserializeAsync(respStream, typeof(AnonymToken), BuildInJsonContext.Default);
            api.AccessToken = atr.Token;
            return api;
        }

        public static async Task<DirectAuthResponse> GetAccessTokenAsync(int clientId, string clientSecret, int scope, string username, string password, string userAgent, string lang = "en", string code = null, string captchaSid = null, string captchaKey = null, Func<Uri, Dictionary<string, string>, Dictionary<string, string>, Task<HttpResponseMessage>> webRequestCallback = null) {
            Dictionary<string, string> p = new Dictionary<string, string> {
                { "grant_type", "password" },
                { "client_id", clientId.ToString() },
                { "client_secret", clientSecret },
                { "username", username },
                { "password", password },
                { "scope", scope.ToString() },
                { "2fa_supported", "1" },
                { "revoke", "1" },
                { "v", VKAPI.Version },
                { "lang", lang }
            };

            if (!String.IsNullOrWhiteSpace(code)) p["code"] = code;
            if (!String.IsNullOrWhiteSpace(captchaSid)) p["captcha_sid"] = captchaSid;
            if (!String.IsNullOrWhiteSpace(captchaKey)) p["captcha_key"] = captchaKey;

            VKAPI api = new VKAPI(null, lang, userAgent);
            api.WebRequestCallback = webRequestCallback;

            using var response = await api.SendRequestAsync(new Uri("https://oauth.vk.com/token"), p);
            using var respStream = await response.ReadAsStreamAsync();

            return (DirectAuthResponse)await JsonSerializer.DeserializeAsync(respStream, typeof(DirectAuthResponse), BuildInJsonContext.Default);
        }
    }
}
