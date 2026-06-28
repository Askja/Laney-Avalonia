using NSec.Cryptography;
using Serilog;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public static class E2EManager {
        public const string PayloadPrefix = "laney-e2e:v1:";
        public const string HandshakePrefix = "laney-e2e-handshake:v1:";
        public const string TrustedBackupPrefix = "laney-e2e-backup:v1:";
        private const int BalancedIterations = 210000;
        private const int ParanoidIterations = 620000;
        private const int LegacyIterations = 160000;
        private const int TrustedBackupIterations = 260000;
        private const int MaxCachedMessageKeys = 96;
        private const string AlgorithmAesGcm = "aes-256-gcm";
        private const string AlgorithmChaCha = "chacha20-poly1305";
        private const string AlgorithmXChaCha = "xchacha20-poly1305";
        private const string AlgorithmAesCtrHmac = "aes-256-ctr+hmac-sha256";
        private const string AlgorithmParanoid = "aes-256-ctr+hmac-sha256+xchacha20-poly1305";
        private const string AlgorithmParanoidFallback = "aes-256-ctr+hmac-sha256+aes-256-gcm";
        private const string AlgorithmParanoidLegacy = "aes-256-gcm+chacha20-poly1305";
        private const string AlgorithmParanoidLegacyFallback = "aes-256-gcm+aes-256-gcm";
        private const string KdfStatic = "pbkdf2-static-v1";
        private const string KdfX25519HkdfChain = "x25519-hkdf-sha256-chain-v1";

        public static E2EPeerState GetPeerState(long peerId) {
            if (peerId == 0) return null;

            string json = Settings.Get($"{Settings.PEER_LOCAL_E2E_STATE_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(json)) return null;

            try {
                E2EPeerState state = JsonSerializer.Deserialize<E2EPeerState>(json);
                if (state != null) state.ProfileId = E2ESecurityProfileIds.Normalize(state.ProfileId);
                return state;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read E2E state for peer {PeerId}", peerId);
                return null;
            }
        }

        public static E2EPeerState ConfigurePeerFromPassphrase(long peerId, string profileId, string passphrase, bool markVerified, bool autoEncryptText = false) {
            if (peerId == 0) throw new ArgumentOutOfRangeException(nameof(peerId));
            if (String.IsNullOrWhiteSpace(passphrase) || passphrase.Trim().Length < 8) {
                throw new ArgumentException("E2E passphrase must contain at least 8 characters.", nameof(passphrase));
            }

            string normalizedProfile = E2ESecurityProfileIds.Normalize(profileId);
            byte[] rootKey = DeriveRootKey(passphrase.Trim(), normalizedProfile);
            byte[] identity = Hmac(rootKey, "identity-public");
            byte[] ratchet = Hmac(rootKey, "ratchet");
            byte[] chain = Hmac(rootKey, "chain");
            string fingerprint = E2EKeyStore.BuildFingerprint(Hmac(rootKey, "fingerprint"));
            string sas = BuildSas(Hmac(rootKey, "sas"));

            E2EPeerState previous = GetPeerState(peerId);
            bool keyChanged = previous != null
                && !String.IsNullOrWhiteSpace(previous.Fingerprint)
                && !String.Equals(previous.Fingerprint, fingerprint, StringComparison.Ordinal);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            E2EPeerKeyMaterial material = new E2EPeerKeyMaterial {
                ProfileId = normalizedProfile,
                IdentityPublicKey = identity,
                IdentityPrivateKey = Hmac(rootKey, "identity-private"),
                RatchetPublicKey = ratchet,
                RatchetPrivateKey = Hmac(rootKey, "ratchet-private"),
                RootKey = rootKey,
                SendingChainKey = chain,
                ReceivingChainKey = chain,
                Fingerprint = fingerprint,
                UsesX25519Handshake = false,
                CreatedAt = previous?.ConfiguredAtUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(previous.ConfiguredAtUnix) : now,
                RotatedAt = keyChanged ? now : previous?.KeyChangedAtUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(previous.KeyChangedAtUnix) : now
            };

            E2EKeyStore.SavePeerKeys(peerId, material);

            E2EPeerState state = new E2EPeerState {
                ProfileId = normalizedProfile,
                Fingerprint = fingerprint,
                Sas = sas,
                IsLaneyPeer = true,
                IsVerified = markVerified && !keyChanged,
                IsKeyChanged = keyChanged,
                AutoEncryptText = autoEncryptText,
                UsesX25519Handshake = false,
                ConfiguredAtUnix = previous?.ConfiguredAtUnix > 0 ? previous.ConfiguredAtUnix : now.ToUnixTimeSeconds(),
                VerifiedAtUnix = markVerified && !keyChanged ? now.ToUnixTimeSeconds() : 0,
                LastPayloadAtUnix = previous?.LastPayloadAtUnix ?? 0,
                KeyChangedAtUnix = keyChanged ? now.ToUnixTimeSeconds() : previous?.KeyChangedAtUnix ?? 0
            };

            SavePeerState(peerId, state);
            return state;
        }

        public static E2EHandshakeResult CreateX25519Handshake(long peerId, string profileId, bool autoEncryptText = false) {
            if (peerId == 0) throw new ArgumentOutOfRangeException(nameof(peerId));

            string normalizedProfile = E2ESecurityProfileIds.Normalize(profileId);
            E2EPeerState previous = GetPeerState(peerId);
            E2EPeerKeyMaterial material = CreateLocalX25519Material(normalizedProfile, previous);
            E2EKeyStore.SavePeerKeys(peerId, material);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            E2EPeerState state = new E2EPeerState {
                ProfileId = normalizedProfile,
                Fingerprint = E2EKeyStore.BuildFingerprint(material.IdentityPublicKey),
                Sas = "ожидает handshake",
                IsLaneyPeer = true,
                IsVerified = false,
                IsKeyChanged = previous != null,
                AutoEncryptText = autoEncryptText || previous?.AutoEncryptText == true,
                UsesX25519Handshake = true,
                RotationCounter = (previous?.RotationCounter ?? 0) + (previous == null ? 0 : 1),
                ConfiguredAtUnix = previous?.ConfiguredAtUnix > 0 ? previous.ConfiguredAtUnix : now.ToUnixTimeSeconds(),
                VerifiedAtUnix = 0,
                LastPayloadAtUnix = previous?.LastPayloadAtUnix ?? 0,
                KeyChangedAtUnix = previous != null ? now.ToUnixTimeSeconds() : 0,
                TrustedBackupCreatedAtUnix = previous?.TrustedBackupCreatedAtUnix ?? 0
            };
            SavePeerState(peerId, state);

            return new E2EHandshakeResult {
                State = state,
                Token = BuildHandshakeToken(material, state)
            };
        }

        public static E2EHandshakeImportResult ImportX25519Handshake(long peerId, string token, bool markVerified, bool autoEncryptText = false) {
            if (peerId == 0) throw new ArgumentOutOfRangeException(nameof(peerId));
            if (!TryReadHandshakeToken(token, out E2EHandshakeEnvelope handshake)) {
                throw new ArgumentException("Handshake-token не распознан. Нужна строка laney-e2e-handshake:v1:...", nameof(token));
            }

            string normalizedProfile = E2ESecurityProfileIds.Normalize(handshake.ProfileId);
            byte[] remoteIdentityPublicKey = Convert.FromBase64String(handshake.IdentityPublicKey);
            byte[] remoteRatchetPublicKey = Convert.FromBase64String(handshake.RatchetPublicKey);

            E2EPeerState previous = GetPeerState(peerId);
            E2EPeerKeyMaterial material = E2EKeyStore.GetPeerKeys(peerId, normalizedProfile);
            if (material == null || !material.UsesX25519Handshake || material.IdentityPrivateKey?.Length == 0 || IsSameKey(material.IdentityPublicKey, remoteIdentityPublicKey)) {
                material = CreateLocalX25519Material(normalizedProfile, previous);
            }

            material.RemoteIdentityPublicKey = remoteIdentityPublicKey;
            material.RemoteRatchetPublicKey = remoteRatchetPublicKey;
            CompleteX25519Material(peerId, material);

            string fingerprint = material.Fingerprint;
            bool keyChanged = previous != null
                && !String.IsNullOrWhiteSpace(previous.Fingerprint)
                && !String.Equals(previous.Fingerprint, fingerprint, StringComparison.Ordinal);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            E2EPeerState state = new E2EPeerState {
                ProfileId = normalizedProfile,
                Fingerprint = fingerprint,
                Sas = BuildSas(material.RootKey),
                IsLaneyPeer = true,
                IsVerified = markVerified && !keyChanged,
                IsKeyChanged = keyChanged,
                AutoEncryptText = autoEncryptText || previous?.AutoEncryptText == true,
                UsesX25519Handshake = true,
                RotationCounter = previous?.RotationCounter ?? 0,
                ConfiguredAtUnix = previous?.ConfiguredAtUnix > 0 ? previous.ConfiguredAtUnix : now.ToUnixTimeSeconds(),
                VerifiedAtUnix = markVerified && !keyChanged ? now.ToUnixTimeSeconds() : 0,
                LastPayloadAtUnix = previous?.LastPayloadAtUnix ?? 0,
                KeyChangedAtUnix = keyChanged ? now.ToUnixTimeSeconds() : previous?.KeyChangedAtUnix ?? 0,
                TrustedBackupCreatedAtUnix = previous?.TrustedBackupCreatedAtUnix ?? material.TrustedBackupCreatedAtUnix
            };

            E2EKeyStore.SavePeerKeys(peerId, material);
            SavePeerState(peerId, state);

            return new E2EHandshakeImportResult {
                State = state,
                ResponseToken = BuildHandshakeToken(material, state)
            };
        }

        public static E2EHandshakeResult RotatePeerX25519Keys(long peerId) {
            E2EPeerState state = GetPeerState(peerId);
            if (state == null) throw new InvalidOperationException("E2E для чата ещё не настроен.");

            return CreateX25519Handshake(peerId, state.ProfileId, state.AutoEncryptText);
        }

        public static E2ETrustedBackupResult ExportTrustedDeviceBackup(long peerId, string passphrase) {
            if (String.IsNullOrWhiteSpace(passphrase) || passphrase.Trim().Length < 8) {
                throw new ArgumentException("Backup-фраза должна быть минимум 8 символов.", nameof(passphrase));
            }

            E2EPeerState state = GetPeerState(peerId);
            E2EPeerKeyMaterial material = state != null ? E2EKeyStore.GetPeerKeys(peerId, state.ProfileId) : null;
            if (state == null || material == null) throw new InvalidOperationException("Для этого чата нет E2E-ключей.");

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            state.TrustedBackupCreatedAtUnix = now;
            material.TrustedBackupCreatedAtUnix = now;
            E2EKeyStore.SavePeerKeys(peerId, material);
            SavePeerState(peerId, state);

            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(passphrase.Trim(), salt, TrustedBackupIterations, HashAlgorithmName.SHA256, 32);
            byte[] plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new E2ETrustedDeviceBackupPayload {
                Version = 1,
                PeerId = peerId,
                State = state,
                Material = material,
                ExportedAtUnix = now
            }));
            E2ECipherLayer layer = SealLayer(key, plain, AlgorithmAesGcm);
            E2ETrustedDeviceBackupEnvelope envelope = new E2ETrustedDeviceBackupEnvelope {
                Version = 1,
                Salt = Convert.ToBase64String(salt),
                Iterations = TrustedBackupIterations,
                Algorithm = layer.Algorithm,
                CipherText = layer.CipherText,
                Nonce = layer.Nonce,
                Tag = layer.Tag
            };

            return new E2ETrustedBackupResult {
                PeerId = peerId,
                Token = $"{TrustedBackupPrefix}{ToBase64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope)))}"
            };
        }

        public static long ImportTrustedDeviceBackup(string token, string passphrase) {
            if (String.IsNullOrWhiteSpace(passphrase) || passphrase.Trim().Length < 8) {
                throw new ArgumentException("Backup-фраза должна быть минимум 8 символов.", nameof(passphrase));
            }
            if (!TryReadTrustedBackupToken(token, out E2ETrustedDeviceBackupEnvelope envelope)) {
                throw new ArgumentException("Backup-token не распознан. Нужна строка laney-e2e-backup:v1:...", nameof(token));
            }

            byte[] salt = Convert.FromBase64String(envelope.Salt);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(passphrase.Trim(), salt, envelope.Iterations, HashAlgorithmName.SHA256, 32);
            byte[] plain = OpenLayer(key, new E2ECipherLayer {
                Algorithm = envelope.Algorithm,
                CipherText = envelope.CipherText,
                Nonce = envelope.Nonce,
                Tag = envelope.Tag
            });
            E2ETrustedDeviceBackupPayload payload = JsonSerializer.Deserialize<E2ETrustedDeviceBackupPayload>(Encoding.UTF8.GetString(plain));
            if (payload?.PeerId == 0 || payload.State == null || payload.Material == null) {
                throw new InvalidOperationException("Backup payload повреждён.");
            }

            payload.State.TrustedBackupCreatedAtUnix = payload.ExportedAtUnix;
            payload.Material.TrustedBackupCreatedAtUnix = payload.ExportedAtUnix;
            E2EKeyStore.SavePeerKeys(payload.PeerId, payload.Material);
            SavePeerState(payload.PeerId, payload.State);
            return payload.PeerId;
        }

        public static void SetAutoEncryptText(long peerId, bool enabled) {
            E2EPeerState state = GetPeerState(peerId);
            if (state == null) return;

            state.AutoEncryptText = enabled;
            SavePeerState(peerId, state);
        }

        public static void SetPeerVerified(long peerId, bool verified) {
            E2EPeerState state = GetPeerState(peerId);
            if (state == null) return;

            state.IsVerified = verified;
            state.IsKeyChanged = verified ? false : state.IsKeyChanged;
            state.VerifiedAtUnix = verified ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0;
            SavePeerState(peerId, state);
        }

        public static void DisablePeer(long peerId) {
            if (peerId == 0) return;

            foreach (string profileId in E2ESecurityProfileIds.All) {
                E2EKeyStore.DeletePeerKeys(peerId, profileId);
            }

            Settings.Set($"{Settings.PEER_LOCAL_E2E_STATE_PREFIX}{peerId}", null);
        }

        public static bool CanEncrypt(long peerId) {
            E2EPeerState state = GetPeerState(peerId);
            E2EPeerKeyMaterial material = state != null ? E2EKeyStore.GetPeerKeys(peerId, state.ProfileId) : null;
            if (material == null) return false;
            if (!material.UsesX25519Handshake) return true;
            return material.RemoteIdentityPublicKey?.Length > 0
                && material.RemoteRatchetPublicKey?.Length > 0
                && material.RootKey?.Length > 0;
        }

        public static bool ShouldAutoEncryptText(long peerId, string text) {
            if (String.IsNullOrWhiteSpace(text)) return false;
            if (TryReadEnvelope(text, out _)) return false;

            E2EPeerState state = GetPeerState(peerId);
            return state?.AutoEncryptText == true && CanEncrypt(peerId);
        }

        public static string EncryptMessage(long peerId, string plainText) {
            if (String.IsNullOrWhiteSpace(plainText)) throw new ArgumentException("Plain text is empty.", nameof(plainText));

            return EncryptPlainPayload(peerId, new E2EPlainPayload {
                Type = E2EPlainPayloadTypes.Text,
                Text = plainText
            });
        }

        public static E2EEncryptedAttachmentFile EncryptAttachmentFile(byte[] plain) {
            if (plain == null || plain.Length == 0) throw new ArgumentException("Attachment is empty.", nameof(plain));

            byte[] fileKey = new byte[32];
            RandomNumberGenerator.Fill(fileKey);
            E2ECipherLayer layer = SealLayer(fileKey, plain, AlgorithmAesGcm);
            E2EEncryptedFileEnvelope fileEnvelope = new E2EEncryptedFileEnvelope {
                Version = 1,
                Algorithm = layer.Algorithm,
                CipherText = layer.CipherText,
                Nonce = layer.Nonce,
                Tag = layer.Tag
            };

            return new E2EEncryptedAttachmentFile {
                Bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fileEnvelope)),
                Key = Convert.ToBase64String(fileKey),
                Algorithm = layer.Algorithm,
                Nonce = layer.Nonce,
                Tag = layer.Tag
            };
        }

        public static string EncryptAttachmentKeyMessage(long peerId, string fileName, long originalSize, E2EEncryptedAttachmentFile encryptedFile) {
            if (encryptedFile == null) throw new ArgumentNullException(nameof(encryptedFile));
            if (String.IsNullOrWhiteSpace(fileName)) fileName = "file";

            return EncryptPlainPayload(peerId, new E2EPlainPayload {
                Type = E2EPlainPayloadTypes.AttachmentKey,
                Attachment = new E2EAttachmentKeyPayload {
                    FileName = fileName,
                    OriginalSize = originalSize,
                    Algorithm = encryptedFile.Algorithm,
                    Key = encryptedFile.Key,
                    Nonce = encryptedFile.Nonce,
                    Tag = encryptedFile.Tag
                }
            });
        }

        private static string EncryptPlainPayload(long peerId, E2EPlainPayload payload) {
            string plainText = JsonSerializer.Serialize(payload);

            E2EPeerState state = GetPeerState(peerId);
            if (state == null) throw new InvalidOperationException("E2E is not configured for this peer.");

            E2EPeerKeyMaterial material = E2EKeyStore.GetPeerKeys(peerId, state.ProfileId);
            if (material?.SendingChainKey == null || material.SendingChainKey.Length == 0) {
                throw new InvalidOperationException("E2E key material is missing.");
            }

            string profileId = E2ESecurityProfileIds.Normalize(state.ProfileId);
            E2EEnvelope envelope = Seal(peerId, profileId, material, Encoding.UTF8.GetBytes(plainText));
            string json = JsonSerializer.Serialize(envelope);
            return $"🔐 Сообщение Laney E2E. В официальном VK тут только зашифрованный payload.\n{PayloadPrefix}{ToBase64Url(Encoding.UTF8.GetBytes(json))}";
        }

        public static bool TryBuildDisplayText(long peerId, string text, out string displayText, out bool encrypted, out bool failed) {
            displayText = text;
            encrypted = false;
            failed = false;

            if (!TryReadEnvelope(text, out E2EEnvelope envelope)) return false;

            encrypted = true;
            MarkPayloadSeen(peerId, envelope.ProfileId, envelope.Fingerprint);

            E2EPeerKeyMaterial material = E2EKeyStore.GetPeerKeys(peerId, envelope.ProfileId);
            if (material == null) {
                failed = true;
                displayText = $"🔐 Laney E2E: нет ключа для профиля {E2ESecurityProfileIds.GetTitle(envelope.ProfileId)}.";
                return true;
            }

            try {
                byte[] plain = Open(peerId, envelope, material);
                displayText = FormatDecryptedText(Encoding.UTF8.GetString(plain));
                return true;
            } catch (Exception ex) {
                failed = true;
                Log.Warning(ex, "Cannot decrypt Laney E2E message for peer {PeerId}", peerId);
                displayText = "🔐 Laney E2E: не удалось расшифровать. Ключ сменился или профиль не тот.";
                return true;
            }
        }

        public static bool TryReadEnvelope(string text, out E2EEnvelope envelope) {
            envelope = null;
            if (String.IsNullOrWhiteSpace(text)) return false;

            int index = text.IndexOf(PayloadPrefix, StringComparison.Ordinal);
            if (index < 0) return false;

            string token = text.Substring(index + PayloadPrefix.Length).Trim();
            int end = token.IndexOfAny(['\r', '\n', ' ', '\t']);
            if (end >= 0) token = token.Substring(0, end);
            if (String.IsNullOrWhiteSpace(token)) return false;

            try {
                string json = Encoding.UTF8.GetString(FromBase64Url(token));
                envelope = JsonSerializer.Deserialize<E2EEnvelope>(json);
                return envelope?.Version == 1 && !String.IsNullOrWhiteSpace(envelope.CipherText);
            } catch {
                envelope = null;
                return false;
            }
        }

        public static void MarkPayloadSeen(long peerId, string profileId, string fingerprint) {
            E2EPeerState state = GetPeerState(peerId) ?? new E2EPeerState {
                ProfileId = E2ESecurityProfileIds.Normalize(profileId)
            };

            string normalizedProfile = E2ESecurityProfileIds.Normalize(profileId);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            state.IsLaneyPeer = true;
            state.ProfileId = normalizedProfile;
            state.LastPayloadAtUnix = now;

            if (!String.IsNullOrWhiteSpace(fingerprint)) {
                if (!String.IsNullOrWhiteSpace(state.Fingerprint)
                    && !String.Equals(state.Fingerprint, fingerprint, StringComparison.Ordinal)) {
                    state.IsKeyChanged = true;
                    state.IsVerified = false;
                    state.KeyChangedAtUnix = now;
                }

                if (String.IsNullOrWhiteSpace(state.Fingerprint)) {
                    state.Fingerprint = fingerprint;
                }
            }

            SavePeerState(peerId, state);
        }

        private static E2EPeerKeyMaterial CreateLocalX25519Material(string profileId, E2EPeerState previous) {
            KeyCreationParameters creationParameters = new KeyCreationParameters {
                ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
            };
            KeyAgreementAlgorithm algorithm = KeyAgreementAlgorithm.X25519;
            using Key identity = Key.Create(algorithm, creationParameters);
            using Key ratchet = Key.Create(algorithm, creationParameters);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            return new E2EPeerKeyMaterial {
                ProfileId = E2ESecurityProfileIds.Normalize(profileId),
                IdentityPublicKey = identity.PublicKey.Export(KeyBlobFormat.RawPublicKey),
                IdentityPrivateKey = identity.Export(KeyBlobFormat.RawPrivateKey),
                RatchetPublicKey = ratchet.PublicKey.Export(KeyBlobFormat.RawPublicKey),
                RatchetPrivateKey = ratchet.Export(KeyBlobFormat.RawPrivateKey),
                SentMessageKeys = new Dictionary<int, string>(),
                ReceivedMessageKeys = new Dictionary<int, string>(),
                Fingerprint = E2EKeyStore.BuildFingerprint(identity.PublicKey.Export(KeyBlobFormat.RawPublicKey)),
                UsesX25519Handshake = true,
                CreatedAt = previous?.ConfiguredAtUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(previous.ConfiguredAtUnix) : now,
                RotatedAt = now,
                TrustedBackupCreatedAtUnix = previous?.TrustedBackupCreatedAtUnix ?? 0
            };
        }

        private static void CompleteX25519Material(long peerId, E2EPeerKeyMaterial material) {
            if (material?.IdentityPrivateKey == null || material.RatchetPrivateKey == null || material.RemoteIdentityPublicKey == null || material.RemoteRatchetPublicKey == null) {
                throw new InvalidOperationException("X25519 handshake incomplete.");
            }

            KeyAgreementAlgorithm algorithm = KeyAgreementAlgorithm.X25519;
            KeyCreationParameters importParameters = new KeyCreationParameters {
                ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
            };
            using Key identityPrivate = Key.Import(algorithm, material.IdentityPrivateKey, KeyBlobFormat.RawPrivateKey, importParameters);
            using Key ratchetPrivate = Key.Import(algorithm, material.RatchetPrivateKey, KeyBlobFormat.RawPrivateKey, importParameters);
            PublicKey remoteIdentity = PublicKey.Import(algorithm, material.RemoteIdentityPublicKey, KeyBlobFormat.RawPublicKey);
            PublicKey remoteRatchet = PublicKey.Import(algorithm, material.RemoteRatchetPublicKey, KeyBlobFormat.RawPublicKey);
            using SharedSecret identityShared = algorithm.Agree(identityPrivate, remoteIdentity) ?? throw new CryptographicException("X25519 identity agreement failed.");
            using SharedSecret ratchetShared = algorithm.Agree(ratchetPrivate, remoteRatchet) ?? throw new CryptographicException("X25519 ratchet agreement failed.");

            string profileId = E2ESecurityProfileIds.Normalize(material.ProfileId);
            byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes($"laney-e2e-x25519-v1:{peerId}:{profileId}"));
            byte[] transcript = BuildHandshakeTranscript(material.IdentityPublicKey, material.RemoteIdentityPublicKey, material.RatchetPublicKey, material.RemoteRatchetPublicKey, profileId);
            byte[] identitySecret = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(identityShared, salt, Combine(transcript, Encoding.UTF8.GetBytes(":identity")), 32);
            byte[] ratchetSecret = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(ratchetShared, salt, Combine(transcript, Encoding.UTF8.GetBytes(":ratchet")), 32);
            byte[] derived = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(Combine(identitySecret, ratchetSecret), salt, Combine(transcript, Encoding.UTF8.GetBytes(":root")), 96);

            byte[] rootKey = derived.Take(32).ToArray();
            byte[] lowToHigh = derived.Skip(32).Take(32).ToArray();
            byte[] highToLow = derived.Skip(64).Take(32).ToArray();
            bool localIsLow = CompareKeys(material.IdentityPublicKey, material.RemoteIdentityPublicKey) <= 0;

            material.RootKey = rootKey;
            material.SendingChainKey = localIsLow ? lowToHigh : highToLow;
            material.ReceivingChainKey = localIsLow ? highToLow : lowToHigh;
            material.SendingMessageNumber = 0;
            material.ReceivingMessageNumber = 0;
            material.SentMessageKeys = new Dictionary<int, string>();
            material.ReceivedMessageKeys = new Dictionary<int, string>();
            material.Fingerprint = E2EKeyStore.BuildFingerprint(rootKey);
            material.UsesX25519Handshake = true;
            material.RotatedAt = DateTimeOffset.UtcNow;
        }

        private static string BuildHandshakeToken(E2EPeerKeyMaterial material, E2EPeerState state) {
            E2EHandshakeEnvelope envelope = new E2EHandshakeEnvelope {
                Version = 1,
                ProfileId = E2ESecurityProfileIds.Normalize(material.ProfileId),
                IdentityPublicKey = Convert.ToBase64String(material.IdentityPublicKey),
                RatchetPublicKey = Convert.ToBase64String(material.RatchetPublicKey),
                KeyId = E2EKeyStore.BuildFingerprint(material.IdentityPublicKey),
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AutoEncryptText = state?.AutoEncryptText == true
            };
            return $"{HandshakePrefix}{ToBase64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope)))}";
        }

        private static bool TryReadHandshakeToken(string text, out E2EHandshakeEnvelope envelope) {
            envelope = null;
            string token = ExtractToken(text, HandshakePrefix);
            if (String.IsNullOrWhiteSpace(token)) return false;

            try {
                string json = Encoding.UTF8.GetString(FromBase64Url(token));
                envelope = JsonSerializer.Deserialize<E2EHandshakeEnvelope>(json);
                return envelope?.Version == 1
                    && !String.IsNullOrWhiteSpace(envelope.IdentityPublicKey)
                    && !String.IsNullOrWhiteSpace(envelope.RatchetPublicKey);
            } catch {
                envelope = null;
                return false;
            }
        }

        private static bool TryReadTrustedBackupToken(string text, out E2ETrustedDeviceBackupEnvelope envelope) {
            envelope = null;
            string token = ExtractToken(text, TrustedBackupPrefix);
            if (String.IsNullOrWhiteSpace(token)) return false;

            try {
                string json = Encoding.UTF8.GetString(FromBase64Url(token));
                envelope = JsonSerializer.Deserialize<E2ETrustedDeviceBackupEnvelope>(json);
                return envelope?.Version == 1
                    && envelope.Iterations > 0
                    && !String.IsNullOrWhiteSpace(envelope.CipherText)
                    && !String.IsNullOrWhiteSpace(envelope.Nonce)
                    && !String.IsNullOrWhiteSpace(envelope.Tag);
            } catch {
                envelope = null;
                return false;
            }
        }

        private static string ExtractToken(string text, string prefix) {
            if (String.IsNullOrWhiteSpace(text)) return null;

            int index = text.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0) return null;

            string token = text.Substring(index + prefix.Length).Trim();
            int end = token.IndexOfAny(['\r', '\n', ' ', '\t']);
            return end >= 0 ? token.Substring(0, end) : token;
        }

        private static byte[] ResolveSendingMessageKey(long peerId, E2EPeerKeyMaterial material, E2EEnvelope envelope) {
            if (!material.UsesX25519Handshake) return material.SendingChainKey;

            int messageNumber = Math.Max(0, material.SendingMessageNumber);
            E2EChainStep step = DeriveChainStep(material.RootKey, material.SendingChainKey, messageNumber);
            material.SendingChainKey = step.NextChainKey;
            material.SendingMessageNumber = messageNumber + 1;
            CacheMessageKey(material.SentMessageKeys ??= new Dictionary<int, string>(), messageNumber, step.MessageKey);
            E2EKeyStore.SavePeerKeys(peerId, material);

            envelope.Kdf = KdfX25519HkdfChain;
            envelope.MessageNumber = messageNumber;
            envelope.SenderKeyId = E2EKeyStore.BuildFingerprint(material.IdentityPublicKey);
            return step.MessageKey;
        }

        private static byte[] ResolveOpeningMessageKey(long peerId, E2EPeerKeyMaterial material, E2EEnvelope envelope) {
            if (!material.UsesX25519Handshake || envelope.MessageNumber < 0 || envelope.Kdf != KdfX25519HkdfChain) {
                return material.ReceivingChainKey?.Length > 0 ? material.ReceivingChainKey : material.SendingChainKey;
            }

            bool ownMessage = !String.IsNullOrWhiteSpace(envelope.SenderKeyId)
                && String.Equals(envelope.SenderKeyId, E2EKeyStore.BuildFingerprint(material.IdentityPublicKey), StringComparison.Ordinal);
            Dictionary<int, string> cache = ownMessage
                ? material.SentMessageKeys ??= new Dictionary<int, string>()
                : material.ReceivedMessageKeys ??= new Dictionary<int, string>();

            if (cache.TryGetValue(envelope.MessageNumber, out string cached)) {
                return Convert.FromBase64String(cached);
            }

            if (ownMessage) throw new CryptographicException("Локальный ключ отправленного E2E-сообщения уже вытеснен из кеша.");
            if (envelope.MessageNumber < material.ReceivingMessageNumber) {
                throw new CryptographicException("Ключ старого E2E-сообщения уже вытеснен из ratchet-кеша.");
            }

            while (material.ReceivingMessageNumber <= envelope.MessageNumber) {
                E2EChainStep step = DeriveChainStep(material.RootKey, material.ReceivingChainKey, material.ReceivingMessageNumber);
                material.ReceivingChainKey = step.NextChainKey;
                CacheMessageKey(cache, material.ReceivingMessageNumber, step.MessageKey);
                material.ReceivingMessageNumber++;
            }

            E2EKeyStore.SavePeerKeys(peerId, material);
            return Convert.FromBase64String(cache[envelope.MessageNumber]);
        }

        private static E2EChainStep DeriveChainStep(byte[] rootKey, byte[] chainKey, int messageNumber) {
            byte[] info = Encoding.UTF8.GetBytes($"laney-e2e-chain-v1:{messageNumber}");
            byte[] derived = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(chainKey, rootKey, info, 64);
            return new E2EChainStep {
                MessageKey = derived.Take(32).ToArray(),
                NextChainKey = derived.Skip(32).Take(32).ToArray()
            };
        }

        private static void CacheMessageKey(Dictionary<int, string> cache, int messageNumber, byte[] key) {
            cache[messageNumber] = Convert.ToBase64String(key);
            foreach (int stale in cache.Keys.OrderByDescending(k => k).Skip(MaxCachedMessageKeys).ToList()) {
                cache.Remove(stale);
            }
        }

        private static byte[] BuildHandshakeTranscript(byte[] localIdentity, byte[] remoteIdentity, byte[] localRatchet, byte[] remoteRatchet, string profileId) {
            byte[] firstIdentity = CompareKeys(localIdentity, remoteIdentity) <= 0 ? localIdentity : remoteIdentity;
            byte[] secondIdentity = ReferenceEquals(firstIdentity, localIdentity) ? remoteIdentity : localIdentity;
            byte[] firstRatchet = CompareKeys(localRatchet, remoteRatchet) <= 0 ? localRatchet : remoteRatchet;
            byte[] secondRatchet = ReferenceEquals(firstRatchet, localRatchet) ? remoteRatchet : localRatchet;
            return Combine(
                Encoding.UTF8.GetBytes($"laney-e2e-x25519-transcript-v1:{E2ESecurityProfileIds.Normalize(profileId)}:"),
                firstIdentity,
                secondIdentity,
                firstRatchet,
                secondRatchet);
        }

        private static int CompareKeys(byte[] left, byte[] right) {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            int length = Math.Min(left.Length, right.Length);
            for (int i = 0; i < length; i++) {
                int comparison = left[i].CompareTo(right[i]);
                if (comparison != 0) return comparison;
            }

            return left.Length.CompareTo(right.Length);
        }

        private static bool IsSameKey(byte[] left, byte[] right) {
            return left != null && right != null && left.SequenceEqual(right);
        }

        private static byte[] Combine(params byte[][] parts) {
            int length = parts.Where(p => p != null).Sum(p => p.Length);
            byte[] combined = new byte[length];
            int offset = 0;
            foreach (byte[] part in parts.Where(p => p != null)) {
                Buffer.BlockCopy(part, 0, combined, offset, part.Length);
                offset += part.Length;
            }

            return combined;
        }

        private static E2EEnvelope Seal(long peerId, string profileId, E2EPeerKeyMaterial material, byte[] plain) {
            E2EEnvelope envelope = new E2EEnvelope {
                Version = 1,
                ProfileId = E2ESecurityProfileIds.Normalize(profileId),
                Fingerprint = material.Fingerprint,
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Kdf = material.UsesX25519Handshake ? KdfX25519HkdfChain : KdfStatic,
                MessageNumber = -1,
                SenderKeyId = E2EKeyStore.BuildFingerprint(material.IdentityPublicKey)
            };
            byte[] key = material.UsesX25519Handshake ? ResolveSendingMessageKey(peerId, material, envelope) : material.SendingChainKey;
            string normalizedProfile = E2ESecurityProfileIds.Normalize(profileId);

            if (normalizedProfile == E2ESecurityProfileIds.Paranoid) {
                E2ECipherLayer inner = SealLayer(Hmac(key, "paranoid-inner"), plain, AlgorithmAesCtrHmac);
                byte[] innerJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(inner));
                string outerAlgorithm = GetPreferredChaChaAlgorithm();
                E2ECipherLayer outer = SealLayer(Hmac(key, "paranoid-outer"), innerJson, outerAlgorithm);
                envelope.Algorithm = outerAlgorithm == AlgorithmXChaCha ? AlgorithmParanoid : AlgorithmParanoidFallback;
                envelope.CipherText = outer.CipherText;
                envelope.Nonce = outer.Nonce;
                envelope.Tag = outer.Tag;
                return envelope;
            }

            string algorithm = ResolveProfileAlgorithm(normalizedProfile);
            E2ECipherLayer layer = SealLayer(key, plain, algorithm);
            envelope.Algorithm = algorithm;
            envelope.CipherText = layer.CipherText;
            envelope.Nonce = layer.Nonce;
            envelope.Tag = layer.Tag;
            return envelope;
        }

        private static string FormatDecryptedText(string plainText) {
            try {
                E2EPlainPayload payload = JsonSerializer.Deserialize<E2EPlainPayload>(plainText);
                if (payload?.Type == E2EPlainPayloadTypes.Text) {
                    return $"🔐 {payload.Text}";
                }

                if (payload?.Type == E2EPlainPayloadTypes.AttachmentKey && payload.Attachment != null) {
                    string size = payload.Attachment.OriginalSize > 0 ? $" · {payload.Attachment.OriginalSize} bytes" : String.Empty;
                    return $"🔐 E2E-вложение: {payload.Attachment.FileName}{size}\nКлюч файла получен в payload. Документ во вложении зашифрован.";
                }
            } catch {
                // Старые payload до typed-обёртки показываем как обычный текст.
            }

            return $"🔐 {plainText}";
        }

        private static byte[] Open(long peerId, E2EEnvelope envelope, E2EPeerKeyMaterial material) {
            string profileId = E2ESecurityProfileIds.Normalize(envelope.ProfileId);
            byte[] key = ResolveOpeningMessageKey(peerId, material, envelope);

            if (profileId == E2ESecurityProfileIds.Paranoid) {
                string outerAlgorithm = ResolveParanoidOuterAlgorithm(envelope.Algorithm);
                byte[] innerJson = OpenLayer(Hmac(key, "paranoid-outer"), new E2ECipherLayer {
                    Algorithm = outerAlgorithm,
                    CipherText = envelope.CipherText,
                    Nonce = envelope.Nonce,
                    Tag = envelope.Tag
                });
                E2ECipherLayer inner = JsonSerializer.Deserialize<E2ECipherLayer>(Encoding.UTF8.GetString(innerJson));
                if (inner == null) throw new CryptographicException("Paranoid E2E inner layer is missing.");
                if (String.IsNullOrWhiteSpace(inner.Algorithm)) {
                    inner.Algorithm = ResolveParanoidInnerAlgorithm(envelope.Algorithm);
                }
                return OpenLayer(Hmac(key, "paranoid-inner"), inner);
            }

            return OpenLayer(key, new E2ECipherLayer {
                Algorithm = envelope.Algorithm,
                CipherText = envelope.CipherText,
                Nonce = envelope.Nonce,
                Tag = envelope.Tag
            });
        }

        private static string ResolveProfileAlgorithm(string profileId) {
            return E2ESecurityProfileIds.Normalize(profileId) switch {
                E2ESecurityProfileIds.LegacyCompatible => AlgorithmAesGcm,
                E2ESecurityProfileIds.AesCtrHmac => AlgorithmAesCtrHmac,
                _ => GetPreferredChaChaAlgorithm()
            };
        }

        private static string GetPreferredChaChaAlgorithm() {
            return System.Security.Cryptography.ChaCha20Poly1305.IsSupported ? AlgorithmXChaCha : AlgorithmAesGcm;
        }

        private static string ResolveParanoidOuterAlgorithm(string envelopeAlgorithm) {
            return envelopeAlgorithm switch {
                AlgorithmParanoid => AlgorithmXChaCha,
                AlgorithmParanoidFallback => AlgorithmAesGcm,
                AlgorithmParanoidLegacy => AlgorithmChaCha,
                AlgorithmParanoidLegacyFallback => AlgorithmAesGcm,
                _ => System.Security.Cryptography.ChaCha20Poly1305.IsSupported ? AlgorithmXChaCha : AlgorithmAesGcm
            };
        }

        private static string ResolveParanoidInnerAlgorithm(string envelopeAlgorithm) {
            return envelopeAlgorithm == AlgorithmParanoid || envelopeAlgorithm == AlgorithmParanoidFallback
                ? AlgorithmAesCtrHmac
                : AlgorithmAesGcm;
        }

        private static E2ECipherLayer SealLayer(byte[] key, byte[] plain, string algorithm) {
            if (algorithm == AlgorithmAesCtrHmac) {
                return SealAesCtrHmacLayer(key, plain);
            }

            int nonceLength = algorithm == AlgorithmXChaCha ? 24 : 12;
            byte[] nonce = RandomNumberGenerator.GetBytes(nonceLength);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[16];

            if (algorithm == AlgorithmXChaCha) {
                EncryptXChaCha20Poly1305(key, nonce, plain, cipher, tag);
            } else if (algorithm == AlgorithmChaCha) {
                using System.Security.Cryptography.ChaCha20Poly1305 cha = new System.Security.Cryptography.ChaCha20Poly1305(key);
                cha.Encrypt(nonce, plain, cipher, tag);
            } else if (algorithm == AlgorithmAesGcm) {
                using AesGcm aes = new AesGcm(key, 16);
                aes.Encrypt(nonce, plain, cipher, tag);
            } else {
                throw new NotSupportedException($"Unsupported E2E algorithm: {algorithm}");
            }

            return new E2ECipherLayer {
                Algorithm = algorithm,
                CipherText = Convert.ToBase64String(cipher),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag)
            };
        }

        private static byte[] OpenLayer(byte[] key, E2ECipherLayer layer) {
            if (layer == null) throw new ArgumentNullException(nameof(layer));

            if (layer.Algorithm == AlgorithmAesCtrHmac) {
                return OpenAesCtrHmacLayer(key, layer);
            }

            byte[] nonce = Convert.FromBase64String(layer.Nonce);
            byte[] cipher = Convert.FromBase64String(layer.CipherText);
            byte[] tag = Convert.FromBase64String(layer.Tag);
            byte[] plain = new byte[cipher.Length];

            if (layer.Algorithm == AlgorithmXChaCha) {
                DecryptXChaCha20Poly1305(key, nonce, cipher, tag, plain);
            } else if (layer.Algorithm == AlgorithmChaCha) {
                using System.Security.Cryptography.ChaCha20Poly1305 cha = new System.Security.Cryptography.ChaCha20Poly1305(key);
                cha.Decrypt(nonce, cipher, tag, plain);
            } else if (layer.Algorithm == AlgorithmAesGcm) {
                using AesGcm aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, cipher, tag, plain);
            } else {
                throw new NotSupportedException($"Unsupported E2E algorithm: {layer.Algorithm}");
            }

            return plain;
        }

        private static E2ECipherLayer SealAesCtrHmacLayer(byte[] key, byte[] plain) {
            byte[] nonce = RandomNumberGenerator.GetBytes(16);
            byte[] cipher = new byte[plain.Length];
            byte[] encryptionKey = Hmac(key, "aes-ctr-hmac-encryption");
            byte[] authenticationKey = Hmac(key, "aes-ctr-hmac-authentication");

            try {
                ApplyAesCtr(encryptionKey, nonce, plain, cipher);
                byte[] tag = ComputeAesCtrHmacTag(authenticationKey, nonce, cipher);
                return new E2ECipherLayer {
                    Algorithm = AlgorithmAesCtrHmac,
                    CipherText = Convert.ToBase64String(cipher),
                    Nonce = Convert.ToBase64String(nonce),
                    Tag = Convert.ToBase64String(tag)
                };
            } finally {
                CryptographicOperations.ZeroMemory(encryptionKey);
                CryptographicOperations.ZeroMemory(authenticationKey);
            }
        }

        private static byte[] OpenAesCtrHmacLayer(byte[] key, E2ECipherLayer layer) {
            byte[] nonce = Convert.FromBase64String(layer.Nonce);
            byte[] cipher = Convert.FromBase64String(layer.CipherText);
            byte[] tag = Convert.FromBase64String(layer.Tag);
            byte[] encryptionKey = Hmac(key, "aes-ctr-hmac-encryption");
            byte[] authenticationKey = Hmac(key, "aes-ctr-hmac-authentication");

            try {
                byte[] expectedTag = ComputeAesCtrHmacTag(authenticationKey, nonce, cipher);
                if (!CryptographicOperations.FixedTimeEquals(expectedTag, tag)) {
                    throw new CryptographicException("AES-CTR+HMAC authentication tag mismatch.");
                }

                byte[] plain = new byte[cipher.Length];
                ApplyAesCtr(encryptionKey, nonce, cipher, plain);
                return plain;
            } finally {
                CryptographicOperations.ZeroMemory(encryptionKey);
                CryptographicOperations.ZeroMemory(authenticationKey);
            }
        }

        private static byte[] ComputeAesCtrHmacTag(byte[] authenticationKey, byte[] nonce, byte[] cipher) {
            using HMACSHA256 hmac = new HMACSHA256(authenticationKey);
            byte[] header = Encoding.UTF8.GetBytes(AlgorithmAesCtrHmac);
            hmac.TransformBlock(header, 0, header.Length, header, 0);
            hmac.TransformBlock(nonce, 0, nonce.Length, nonce, 0);
            hmac.TransformFinalBlock(cipher, 0, cipher.Length);
            return hmac.Hash;
        }

        private static void ApplyAesCtr(byte[] key, byte[] nonce, byte[] input, byte[] output) {
            if (nonce.Length != 16) throw new CryptographicException("AES-CTR nonce must be 16 bytes.");
            if (input.Length != output.Length) throw new ArgumentException("AES-CTR input and output lengths must match.", nameof(output));

            using Aes aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.KeySize = 256;
            aes.Key = key;

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] counter = nonce.ToArray();
            byte[] streamBlock = new byte[16];

            for (int offset = 0; offset < input.Length; offset += 16) {
                encryptor.TransformBlock(counter, 0, counter.Length, streamBlock, 0);
                int blockLength = Math.Min(16, input.Length - offset);
                for (int i = 0; i < blockLength; i++) {
                    output[offset + i] = (byte)(input[offset + i] ^ streamBlock[i]);
                }

                IncrementCounter(counter);
            }

            CryptographicOperations.ZeroMemory(counter);
            CryptographicOperations.ZeroMemory(streamBlock);
        }

        private static void IncrementCounter(byte[] counter) {
            for (int i = counter.Length - 1; i >= 0; i--) {
                counter[i]++;
                if (counter[i] != 0) break;
            }
        }

        private static void EncryptXChaCha20Poly1305(byte[] key, byte[] nonce, byte[] plain, byte[] cipher, byte[] tag) {
            byte[] subKey = DeriveXChaChaSubKey(key, nonce, out byte[] chachaNonce);
            try {
                using System.Security.Cryptography.ChaCha20Poly1305 cha = new System.Security.Cryptography.ChaCha20Poly1305(subKey);
                cha.Encrypt(chachaNonce, plain, cipher, tag);
            } finally {
                CryptographicOperations.ZeroMemory(subKey);
                CryptographicOperations.ZeroMemory(chachaNonce);
            }
        }

        private static void DecryptXChaCha20Poly1305(byte[] key, byte[] nonce, byte[] cipher, byte[] tag, byte[] plain) {
            byte[] subKey = DeriveXChaChaSubKey(key, nonce, out byte[] chachaNonce);
            try {
                using System.Security.Cryptography.ChaCha20Poly1305 cha = new System.Security.Cryptography.ChaCha20Poly1305(subKey);
                cha.Decrypt(chachaNonce, cipher, tag, plain);
            } finally {
                CryptographicOperations.ZeroMemory(subKey);
                CryptographicOperations.ZeroMemory(chachaNonce);
            }
        }

        private static byte[] DeriveXChaChaSubKey(byte[] key, byte[] nonce, out byte[] chachaNonce) {
            if (key == null || key.Length != 32) throw new CryptographicException("XChaCha20-Poly1305 key must be 32 bytes.");
            if (nonce == null || nonce.Length != 24) throw new CryptographicException("XChaCha20-Poly1305 nonce must be 24 bytes.");

            byte[] subKey = HChaCha20(key, nonce.AsSpan(0, 16));
            chachaNonce = new byte[12];
            Buffer.BlockCopy(nonce, 16, chachaNonce, 4, 8);
            return subKey;
        }

        private static byte[] HChaCha20(byte[] key, ReadOnlySpan<byte> nonce) {
            uint[] state = new uint[16];
            state[0] = 0x61707865;
            state[1] = 0x3320646e;
            state[2] = 0x79622d32;
            state[3] = 0x6b206574;

            for (int i = 0; i < 8; i++) {
                state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(i * 4, 4));
            }

            for (int i = 0; i < 4; i++) {
                state[12 + i] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(i * 4, 4));
            }

            for (int i = 0; i < 10; i++) {
                ChaChaQuarterRound(state, 0, 4, 8, 12);
                ChaChaQuarterRound(state, 1, 5, 9, 13);
                ChaChaQuarterRound(state, 2, 6, 10, 14);
                ChaChaQuarterRound(state, 3, 7, 11, 15);
                ChaChaQuarterRound(state, 0, 5, 10, 15);
                ChaChaQuarterRound(state, 1, 6, 11, 12);
                ChaChaQuarterRound(state, 2, 7, 8, 13);
                ChaChaQuarterRound(state, 3, 4, 9, 14);
            }

            byte[] output = new byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0, 4), state[0]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4, 4), state[1]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(8, 4), state[2]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(12, 4), state[3]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(16, 4), state[12]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(20, 4), state[13]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(24, 4), state[14]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28, 4), state[15]);
            Array.Clear(state, 0, state.Length);
            return output;
        }

        private static void ChaChaQuarterRound(uint[] state, int a, int b, int c, int d) {
            state[a] += state[b]; state[d] = RotateLeft(state[d] ^ state[a], 16);
            state[c] += state[d]; state[b] = RotateLeft(state[b] ^ state[c], 12);
            state[a] += state[b]; state[d] = RotateLeft(state[d] ^ state[a], 8);
            state[c] += state[d]; state[b] = RotateLeft(state[b] ^ state[c], 7);
        }

        private static uint RotateLeft(uint value, int count) {
            return (value << count) | (value >> (32 - count));
        }

        private static byte[] DeriveRootKey(string passphrase, string profileId) {
            int iterations = E2ESecurityProfileIds.Normalize(profileId) switch {
                E2ESecurityProfileIds.Paranoid => ParanoidIterations,
                E2ESecurityProfileIds.LegacyCompatible => LegacyIterations,
                _ => BalancedIterations
            };
            byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes($"laney-e2e-v1:{E2ESecurityProfileIds.Normalize(profileId)}"));
            return Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, 32);
        }

        private static byte[] Hmac(byte[] key, string label) {
            using HMACSHA256 hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(label));
        }

        private static string BuildSas(byte[] data) {
            if (data == null || data.Length < 6) return "000-000-000";

            int a = BitConverter.ToUInt16(data, 0) % 1000;
            int b = BitConverter.ToUInt16(data, 2) % 1000;
            int c = BitConverter.ToUInt16(data, 4) % 1000;
            return $"{a:D3}-{b:D3}-{c:D3}";
        }

        private static void SavePeerState(long peerId, E2EPeerState state) {
            if (state == null) {
                Settings.Set($"{Settings.PEER_LOCAL_E2E_STATE_PREFIX}{peerId}", null);
                return;
            }

            state.ProfileId = E2ESecurityProfileIds.Normalize(state.ProfileId);
            Settings.Set($"{Settings.PEER_LOCAL_E2E_STATE_PREFIX}{peerId}", JsonSerializer.Serialize(state));
        }

        private static string ToBase64Url(byte[] data) {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] FromBase64Url(string value) {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            int padding = base64.Length % 4;
            if (padding > 0) base64 = base64.PadRight(base64.Length + 4 - padding, '=');
            return Convert.FromBase64String(base64);
        }
    }

    public sealed class E2EEnvelope {
        public int Version { get; set; }
        public string ProfileId { get; set; }
        public string Algorithm { get; set; }
        public string Fingerprint { get; set; }
        public string Kdf { get; set; }
        public int MessageNumber { get; set; } = -1;
        public string SenderKeyId { get; set; }
        public long CreatedAtUnix { get; set; }
        public string CipherText { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }

    public sealed class E2ECipherLayer {
        public string Algorithm { get; set; }
        public string CipherText { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }

    public static class E2EPlainPayloadTypes {
        public const string Text = "text";
        public const string AttachmentKey = "attachment-key";
    }

    public sealed class E2EPlainPayload {
        public string Type { get; set; }
        public string Text { get; set; }
        public E2EAttachmentKeyPayload Attachment { get; set; }
    }

    public sealed class E2EAttachmentKeyPayload {
        public string FileName { get; set; }
        public long OriginalSize { get; set; }
        public string Algorithm { get; set; }
        public string Key { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }

    public sealed class E2EEncryptedAttachmentFile {
        public byte[] Bytes { get; set; }
        public string Key { get; set; }
        public string Algorithm { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }

    public sealed class E2EEncryptedFileEnvelope {
        public int Version { get; set; }
        public string Algorithm { get; set; }
        public string CipherText { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }

    public sealed class E2EHandshakeResult {
        public E2EPeerState State { get; set; }
        public string Token { get; set; }
    }

    public sealed class E2EHandshakeImportResult {
        public E2EPeerState State { get; set; }
        public string ResponseToken { get; set; }
    }

    public sealed class E2ETrustedBackupResult {
        public long PeerId { get; set; }
        public string Token { get; set; }
    }

    public sealed class E2EHandshakeEnvelope {
        public int Version { get; set; }
        public string ProfileId { get; set; }
        public string IdentityPublicKey { get; set; }
        public string RatchetPublicKey { get; set; }
        public string KeyId { get; set; }
        public long CreatedAtUnix { get; set; }
        public bool AutoEncryptText { get; set; }
    }

    public sealed class E2ETrustedDeviceBackupEnvelope {
        public int Version { get; set; }
        public string Salt { get; set; }
        public int Iterations { get; set; }
        public string Algorithm { get; set; }
        public string CipherText { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }

    public sealed class E2ETrustedDeviceBackupPayload {
        public int Version { get; set; }
        public long PeerId { get; set; }
        public E2EPeerState State { get; set; }
        public E2EPeerKeyMaterial Material { get; set; }
        public long ExportedAtUnix { get; set; }
    }

    internal sealed class E2EChainStep {
        public byte[] MessageKey { get; set; }
        public byte[] NextChainKey { get; set; }
    }
}
