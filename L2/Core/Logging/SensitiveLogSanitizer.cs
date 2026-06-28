using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ELOR.Laney.Core.Logging {
    public static class SensitiveLogSanitizer {
        private const string Redacted = "[REDACTED]";
        private static readonly HashSet<string> SensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "access_token",
            "refresh_token",
            "super_app_token",
            "token",
            "session_token",
            "captcha_token",
            "success_token",
            "auth_user_hash",
            "client_secret",
            "captcha_key",
            "key",
            "hash",
            "payload",
            "message",
            "text",
            "keyboard",
            "forward",
            "attachment",
            "attachments",
            "password",
            "passphrase",
            "secret",
            "authorization"
        };

        private static readonly Regex JsonStringSecret = new Regex(
            "\"(?<key>access_token|refresh_token|super_app_token|token|session_token|captcha_token|success_token|auth_user_hash|client_secret|captcha_key|key|hash|payload|message|text|keyboard|forward|attachment|attachments|password|passphrase|secret|authorization)\"\\s*:\\s*\"(?<value>[^\"]*)\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex KeyValueSecret = new Regex(
            "\\b(?<key>access_token|refresh_token|super_app_token|token|session_token|captcha_token|success_token|auth_user_hash|client_secret|captcha_key|key|hash|payload|message|text|keyboard|forward|attachment|attachments|password|passphrase|secret|authorization)\\s*(?<sep>[:=])\\s*(?<value>[^\\s;&,}]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex BearerSecret = new Regex(
            "\\b(?<prefix>Bearer\\s+)(?<value>[A-Za-z0-9._~+/=-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex UrlSecret = new Regex(
            "(?<key>[?&#](?:access_token|refresh_token|super_app_token|token|session_token|captcha_token|success_token|auth_user_hash|client_secret|captcha_key|key|hash|payload|message|text|keyboard|forward|attachment|attachments|password|passphrase|secret|authorization)=)(?<value>[^&#\\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex LaneyToken = new Regex(
            "\\b(?:laney-e2e|laney-e2e-handshake|laney-e2e-backup):v1:[A-Za-z0-9_\\-+=/]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsSensitiveKey(string key) {
            return !String.IsNullOrWhiteSpace(key) && SensitiveKeys.Contains(key);
        }

        public static string Sanitize(string value) {
            if (String.IsNullOrEmpty(value)) return value;

            string sanitized = JsonStringSecret.Replace(value, m => $"\"{m.Groups["key"].Value}\":\"{Redacted}\"");
            sanitized = UrlSecret.Replace(sanitized, m => $"{m.Groups["key"].Value}{Redacted}");
            sanitized = LaneyToken.Replace(sanitized, Redacted);
            sanitized = BearerSecret.Replace(sanitized, m => $"{m.Groups["prefix"].Value}{Redacted}");
            sanitized = KeyValueSecret.Replace(sanitized, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{Redacted}");
            return sanitized;
        }
    }
}
