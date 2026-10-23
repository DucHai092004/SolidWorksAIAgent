using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SwMateAI.Updater
{
    internal sealed class ReleaseInfo
    {
        public Version Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Sha256 { get; set; }
    }

    /// <summary>
    /// Reads only the official GitHub Releases feed. Never executes an update
    /// without a SHA-256 digest provided by GitHub's asset metadata.
    /// </summary>
    internal static class UpdateService
    {
        private const string ApiUrl =
            "https://api.github.com/repos/DucHai092004/SolidWorksAIAgent/releases/latest";
        private const string AssetPrefix = "SW-MATE-AI-Setup-";
        internal static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SW-MATE AI", "Updates");

        internal static Version InstalledVersion =>
            Assembly.GetExecutingAssembly().GetName().Version;

        internal static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Trim().TrimStart('v', 'V').Split('-')[0];
            return Version.TryParse(normalized, out version);
        }

        internal static async Task<ReleaseInfo> GetLatestAsync()
        {
            using (var client = CreateClient(TimeSpan.FromSeconds(20)))
            {
                using (var response = await client.GetAsync(ApiUrl).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new InvalidOperationException(
                            "Chưa có GitHub Release công khai. Cần phát hành bản cài trước khi cập nhật online.");
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ParseRelease(json);
                }
            }
        }

        internal static ReleaseInfo ParseRelease(string json)
        {
            var root = new JavaScriptSerializer().DeserializeObject(json)
                as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("tag_name") ||
                !TryParseVersion(Convert.ToString(root["tag_name"]), out var version))
                throw new InvalidDataException("Release tag không phải phiên bản hợp lệ.");

            var assets = root.ContainsKey("assets") ? root["assets"] as object[] : null;
            if (assets == null) throw new InvalidDataException("Release không có file cài đặt.");
            foreach (var item in assets)
            {
                var asset = item as Dictionary<string, object>;
                if (asset == null) continue;
                string name = Value(asset, "name");
                string expectedName = AssetPrefix + version.ToString(3) + ".exe";
                if (!name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)) continue;

                string url = Value(asset, "browser_download_url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                    !uri.AbsolutePath.StartsWith(
                        "/DucHai092004/SolidWorksAIAgent/releases/download/",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Đường dẫn cập nhật không hợp lệ.");

                string digest = Value(asset, "digest");
                if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Release chưa có SHA-256 digest. Không tải file thiếu xác minh.");
                string sha = digest.Substring(7).ToLowerInvariant();
                if (!IsSha256(sha))
                    throw new InvalidDataException("SHA-256 digest không hợp lệ.");
                return new ReleaseInfo { Version = version, DownloadUrl = url, Sha256 = sha };
            }
            throw new InvalidDataException("Không tìm thấy SW-MATE-AI-Setup-*.exe trong Release.");
        }

        private static string Value(Dictionary<string, object> source, string key) =>
            source.TryGetValue(key, out var value) ? Convert.ToString(value) ?? "" : "";

        internal static bool IsSha256(string value) =>
            value != null && value.Length == 64 &&
            value.All(c => (c >= '0' && c <= '9') ||
                           (c >= 'a' && c <= 'f') ||
                           (c >= 'A' && c <= 'F'));

        internal static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", "").ToLowerInvariant();
        }

        internal static async Task<string> DownloadAsync(ReleaseInfo release, IProgress<int> progress)
        {
            Directory.CreateDirectory(CacheDirectory);
            string target = Path.Combine(CacheDirectory,
                AssetPrefix + release.Version.ToString(3) + ".exe");
            string partial = target + ".partial";
            if (File.Exists(target) &&
                HashFile(target).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                return target;

            try
            {
                using (var client = CreateClient(TimeSpan.FromMinutes(30)))
                using (var response = await client.GetAsync(
                    release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long? total = response.Content.Headers.ContentLength;
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[65536];
                        long received = 0;
                        int count;
                        while ((count = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, count).ConfigureAwait(false);
                            received += count;
                            if (total.HasValue && total.Value > 0)
                                progress?.Report((int)Math.Min(100, received * 100 / total.Value));
                        }
                    }
                }
                if (!HashFile(partial).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256 không khớp. Đã hủy cập nhật.");
                if (File.Exists(target)) File.Delete(target);
                File.Move(partial, target);
                progress?.Report(100);
                return target;
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
        }

        private static HttpClient CreateClient(TimeSpan timeout)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SW-MATE-AI-Updater/0.2");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }
    }
}
