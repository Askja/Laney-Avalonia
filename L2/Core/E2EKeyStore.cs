using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public static class E2ESecurityProfileIds {
        public const string Balanced = "balanced";
        public const string Paranoid = "paranoid";
        public const string LegacyCompatible = "legacy-compatible";
        public const string AesCtrHmac = "aes-ctr-hmac";

        public static IReadOnlyList<string> All { get; } = [
            Balanced,
            Paranoid,
            LegacyCompatible,
            AesCtrHmac
        ];

        public static string Normalize(string profileId) {
            if (String.IsNullOrWhiteSpace(profileId)) return Balanced;

            string normalized = profileId.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Balanced;
        }

        public static string GetTitle(string profileId) {
            return Normalize(profileId) switch {
                Paranoid => "Paranoid double layer",
                LegacyCompatible => "AES-256-GCM",
                AesCtrHmac => "AES-256-CTR + HMAC-SHA256",
                _ => "XChaCha20-Poly1305"
            };
        }

        public static string GetSubtitle(string profileId) {
            return Normalize(profileId) switch {
                Paranoid => "AES-CTR+HMAC внутри, XChaCha20-Poly1305 снаружи; fallback на AES-GCM",
                LegacyCompatible => "Нативный AES-GCM без ChaCha-зависимости",
                AesCtrHmac => "CTR шифрует, HMAC-SHA256 проверяет целостность без иллюзий",
                _ => "24-byte nonce через XChaCha20-Poly1305; fallback на AES-GCM"
            };
        }
    }

    public sealed class E2EPeerKeyMaterial {
        public string ProfileId { get; set; }
        public byte[] IdentityPublicKey { get; set; }
        public byte[] IdentityPrivateKey { get; set; }
        public byte[] RatchetPublicKey { get; set; }
        public byte[] RatchetPrivateKey { get; set; }
        public byte[] RemoteIdentityPublicKey { get; set; }
        public byte[] RemoteRatchetPublicKey { get; set; }
        public byte[] RootKey { get; set; }
        public byte[] SendingChainKey { get; set; }
        public byte[] ReceivingChainKey { get; set; }
        public int SendingMessageNumber { get; set; }
        public int ReceivingMessageNumber { get; set; }
        public Dictionary<int, string> SentMessageKeys { get; set; }
        public Dictionary<int, string> ReceivedMessageKeys { get; set; }
        public string Fingerprint { get; set; }
        public bool UsesX25519Handshake { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset RotatedAt { get; set; }
        public long TrustedBackupCreatedAtUnix { get; set; }
    }

    public sealed class E2EPeerState {
        public string ProfileId { get; set; }
        public string Fingerprint { get; set; }
        public string Sas { get; set; }
        public bool IsLaneyPeer { get; set; }
        public bool IsVerified { get; set; }
        public bool IsKeyChanged { get; set; }
        public bool AutoEncryptText { get; set; }
        public bool UsesX25519Handshake { get; set; }
        public int RotationCounter { get; set; }
        public long ConfiguredAtUnix { get; set; }
        public long VerifiedAtUnix { get; set; }
        public long LastPayloadAtUnix { get; set; }
        public long KeyChangedAtUnix { get; set; }
        public long TrustedBackupCreatedAtUnix { get; set; }
    }

    public static class E2EKeyStore {
        public static void SavePeerKeys(long peerId, E2EPeerKeyMaterial material) {
            if (peerId == 0) throw new ArgumentOutOfRangeException(nameof(peerId));
            if (material == null) throw new ArgumentNullException(nameof(material));

            material.ProfileId = E2ESecurityProfileIds.Normalize(material.ProfileId);
            if (String.IsNullOrWhiteSpace(material.Fingerprint) && material.IdentityPublicKey?.Length > 0) {
                material.Fingerprint = BuildFingerprint(material.IdentityPublicKey);
            }

            if (material.CreatedAt == default) material.CreatedAt = DateTimeOffset.UtcNow;
            if (material.RotatedAt == default) material.RotatedAt = material.CreatedAt;
            material.SentMessageKeys ??= new Dictionary<int, string>();
            material.ReceivedMessageKeys ??= new Dictionary<int, string>();

            string json = JsonSerializer.Serialize(material);
            SecureVault.SetSecret(BuildScopedSecretName(peerId, material.ProfileId), json);
        }

        public static E2EPeerKeyMaterial GetPeerKeys(long peerId, string profileId = E2ESecurityProfileIds.Balanced) {
            if (peerId == 0) return null;

            string normalizedProfileId = E2ESecurityProfileIds.Normalize(profileId);
            string json = SecureVault.GetSecret(BuildScopedSecretName(peerId, normalizedProfileId))
                ?? SecureVault.GetSecret(BuildLegacySecretName(peerId, normalizedProfileId));
            if (String.IsNullOrWhiteSpace(json)) return null;

            E2EPeerKeyMaterial material = JsonSerializer.Deserialize<E2EPeerKeyMaterial>(json);
            if (material != null) {
                material.ProfileId = E2ESecurityProfileIds.Normalize(material.ProfileId);
                material.SentMessageKeys ??= new Dictionary<int, string>();
                material.ReceivedMessageKeys ??= new Dictionary<int, string>();
            }

            return material;
        }

        public static void DeletePeerKeys(long peerId, string profileId = E2ESecurityProfileIds.Balanced) {
            if (peerId == 0) return;
            string normalizedProfileId = E2ESecurityProfileIds.Normalize(profileId);
            SecureVault.DeleteSecret(BuildScopedSecretName(peerId, normalizedProfileId));
            SecureVault.DeleteSecret(BuildLegacySecretName(peerId, normalizedProfileId));
        }

        public static bool HasPeerKeys(long peerId, string profileId = E2ESecurityProfileIds.Balanced) {
            return GetPeerKeys(peerId, profileId) != null;
        }

        public static string BuildFingerprint(byte[] publicKey) {
            if (publicKey == null || publicKey.Length == 0) return String.Empty;

            string hex = Convert.ToHexString(SHA256.HashData(publicKey));
            StringBuilder fingerprint = new StringBuilder(hex.Length + hex.Length / 4);
            for (int i = 0; i < hex.Length; i += 4) {
                if (fingerprint.Length > 0) fingerprint.Append(':');
                int length = Math.Min(4, hex.Length - i);
                fingerprint.Append(hex, i, length);
            }

            return fingerprint.ToString();
        }

        private static string BuildScopedSecretName(long peerId, string profileId) {
            return LocalDataProfile.BuildScopedSecretName(BuildLegacySecretName(peerId, profileId));
        }

        private static string BuildLegacySecretName(long peerId, string profileId) {
            return $"e2e.peer.{E2ESecurityProfileIds.Normalize(profileId)}.{peerId}";
        }
    }
}
