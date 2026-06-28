using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public static class SecureVault {
        private const string DpapiProvider = "dpapi-current-user-v1";
        private const string LocalProvider = "laney-local-v1";
        private const int CryptProtectUiForbidden = 0x1;
        private static readonly string DirectoryPath = Path.Combine(App.LocalDataPath, "vault");

        public static void SetSecret(string name, string value) {
            if (value == null) {
                DeleteSecret(name);
                return;
            }

            SetBytes(name, Encoding.UTF8.GetBytes(value));
        }

        public static string GetSecret(string name) {
            byte[] bytes = GetBytes(name);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        }

        public static void SetBytes(string name, byte[] value) {
            if (value == null) {
                DeleteSecret(name);
                return;
            }

            Directory.CreateDirectory(DirectoryPath);

            VaultRecord record = OperatingSystem.IsWindows()
                ? new VaultRecord {
                    Provider = DpapiProvider,
                    Payload = Convert.ToBase64String(ProtectWithDpapi(value))
                }
                : CreateLocalRecord(value);

            string path = GetSecretPath(name);
            string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            string json = SerializeRecord(record);
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, path, true);
        }

        public static byte[] GetBytes(string name) {
            string path = GetSecretPath(name);
            if (!File.Exists(path)) return null;

            try {
                string json = File.ReadAllText(path, Encoding.UTF8);
                VaultRecord record = DeserializeRecord(json);
                if (record == null) return null;

                if (record.Provider == DpapiProvider) {
                    return UnprotectWithDpapi(Convert.FromBase64String(record.Payload));
                }

                if (record.Provider == LocalProvider) {
                    string protectedBase64 = Encryption.Decrypt(GetLocalVaultKey(), record.Payload, record.Nonce, record.Tag);
                    return Convert.FromBase64String(protectedBase64);
                }

                Log.Warning("Unsupported secure vault provider: {Provider}", record.Provider);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read secure vault secret {SecretName}", name);
            }

            return null;
        }

        public static void DeleteSecret(string name) {
            string path = GetSecretPath(name);
            if (!File.Exists(path)) return;

            try {
                File.Delete(path);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot delete secure vault secret {SecretName}", name);
            }
        }

        private static VaultRecord CreateLocalRecord(byte[] value) {
            var encrypted = Encryption.Encrypt(GetLocalVaultKey(), Convert.ToBase64String(value));
            return new VaultRecord {
                Provider = LocalProvider,
                Payload = encrypted.Item1,
                Nonce = encrypted.Item2,
                Tag = encrypted.Item3
            };
        }

        private static string SerializeRecord(VaultRecord record) {
            using MemoryStream stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            WriteString(writer, nameof(VaultRecord.Provider), record.Provider);
            WriteString(writer, nameof(VaultRecord.Payload), record.Payload);
            WriteString(writer, nameof(VaultRecord.Nonce), record.Nonce);
            WriteString(writer, nameof(VaultRecord.Tag), record.Tag);
            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static VaultRecord DeserializeRecord(string json) {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            return new VaultRecord {
                Provider = ReadString(root, nameof(VaultRecord.Provider)),
                Payload = ReadString(root, nameof(VaultRecord.Payload)),
                Nonce = ReadString(root, nameof(VaultRecord.Nonce)),
                Tag = ReadString(root, nameof(VaultRecord.Tag))
            };
        }

        private static void WriteString(Utf8JsonWriter writer, string name, string value) {
            if (value == null) {
                writer.WriteNull(name);
                return;
            }

            writer.WriteString(name, value);
        }

        private static string ReadString(JsonElement root, string name) {
            return root.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null
                ? value.GetString()
                : null;
        }

        private static string GetSecretPath(string name) {
            string safeName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).ToLowerInvariant();
            return Path.Combine(DirectoryPath, $"{safeName}.secret");
        }

        private static byte[] GetLocalVaultKey() {
            byte[] material = AssetsManager.BinaryPayload.Skip(576).Take(32).OrderDescending().ToArray();
            return SHA256.HashData(material);
        }

        private static byte[] ProtectWithDpapi(byte[] value) {
            if (!OperatingSystem.IsWindows()) {
                throw new PlatformNotSupportedException("DPAPI is available only on Windows.");
            }

            DataBlob input = CreateBlob(value);
            try {
                if (!CryptProtectData(ref input, "Laney secure vault", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob output)) {
                    throw CreateCryptographicException();
                }

                return ConsumeBlob(output);
            } finally {
                FreeInputBlob(input);
            }
        }

        private static byte[] UnprotectWithDpapi(byte[] value) {
            if (!OperatingSystem.IsWindows()) {
                throw new PlatformNotSupportedException("DPAPI is available only on Windows.");
            }

            DataBlob input = CreateBlob(value);
            try {
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob output)) {
                    throw CreateCryptographicException();
                }

                return ConsumeBlob(output);
            } finally {
                FreeInputBlob(input);
            }
        }

        private static DataBlob CreateBlob(byte[] value) {
            IntPtr buffer = Marshal.AllocHGlobal(value.Length);
            Marshal.Copy(value, 0, buffer, value.Length);
            return new DataBlob {
                DataLength = value.Length,
                Data = buffer
            };
        }

        private static byte[] ConsumeBlob(DataBlob blob) {
            try {
                byte[] result = new byte[blob.DataLength];
                Marshal.Copy(blob.Data, result, 0, blob.DataLength);
                return result;
            } finally {
                FreeOutputBlob(blob);
            }
        }

        private static void FreeInputBlob(DataBlob blob) {
            if (blob.Data == IntPtr.Zero) return;
            Marshal.FreeHGlobal(blob.Data);
        }

        private static void FreeOutputBlob(DataBlob blob) {
            if (blob.Data == IntPtr.Zero) return;
            LocalFree(blob.Data);
        }

        private static CryptographicException CreateCryptographicException() {
            return new CryptographicException(Marshal.GetLastWin32Error());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob {
            public int DataLength;
            public IntPtr Data;
        }

        private sealed class VaultRecord {
            public string Provider { get; set; }
            public string Payload { get; set; }
            public string Nonce { get; set; }
            public string Tag { get; set; }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string dataDescription, IntPtr optionalEntropy, IntPtr reserved, IntPtr promptStruct, int flags, out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr dataDescription, IntPtr optionalEntropy, IntPtr reserved, IntPtr promptStruct, int flags, out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);
    }
}
