/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Simulator.Editor
{
    internal class VersionUtils
    {
        private string CacheDirectory => Path.Combine(Path.GetTempPath(), "Meta", "Unity", "Editor");
        private string CacheFilePath => Path.Combine(CacheDirectory, "meta_xr_sim_versions.json");


        private readonly string _lastDownloadTimestampKey = $"Meta.XR.Simulator.LastDownloadTimestamp.Versions";
        public const double DefaultCacheDurationInHours = 24;
        private double _cacheDurationInHours = DefaultCacheDurationInHours;

        private List<Binary> _availableVersions = new();
        private string[] _installedVersions = null;

        public VersionUtils()
        {
#pragma warning disable CS4014
            InitializeAsync();
#pragma warning restore CS4014
        }
        private async Task InitializeAsync()
        {
            ReloadInstalledVersions();

            var successfulLoad = await ReloadAvailableVersions(forceRedownload: false);
            if (!successfulLoad)
            {
                await ReloadAvailableVersions(forceRedownload: true);
            }
        }

        public async Task<bool> ReloadAvailableVersions(bool forceRedownload)
        {
            if (forceRedownload)
            {
                _availableVersions.Clear();
                ClearCache();
            }

            var (success, jsonData) = await RefreshAndLoadAvailableVersions();
            return success && LoadContentJsonData(jsonData);
        }

        public bool ReloadInstalledVersions()
        {
            _installedVersions = null;
            return LoadInstalledVersions();
        }

        public virtual string[] TryToGetInstalledVersions(bool refresh = false)
        {
            if (refresh)
            {
                ReloadInstalledVersions();
            }

            return _installedVersions;
        }

        public virtual async Task<List<Binary>> TryToGetAvailableVersions(bool refresh = false)
        {
            await ReloadAvailableVersions(forceRedownload: refresh);
            return _availableVersions;
        }

        public virtual List<Binary> GetAvailableVersionsFromCache()
        {
            if (!Utils.SystemUtils.FileExists(CacheFilePath))
            {
                return _availableVersions;
            }

            var jsonData = File.ReadAllText(CacheFilePath);
            LoadContentJsonData(jsonData);
            return _availableVersions;
        }

        public virtual async Task<Binary?> TryToLatestAvailableVersion(bool refresh = false)
        {
            await ReloadAvailableVersions(_availableVersions == null || refresh);

            if (_availableVersions == null || _availableVersions.Count == 0)
            {
                return null;
            }

            return _availableVersions.OrderBy(x => x.version, Comparer<string>.Create(Utils.CompareVersions)).LastOrDefault();
        }



        #region Installed Versions

        private bool LoadInstalledVersions()
        {
            if (!Utils.SystemUtils.DirectoryExists(XRSimConstants.XrSimDataPath))
            {
                // no version is installed
                return true;
            }

            try
            {
                _installedVersions = Utils.SystemUtils.ListSubdirectories(XRSimConstants.XrSimDataPath);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public virtual bool IsVersionInstalled(string binaryVersion)
        {
            return _installedVersions != null && _installedVersions.Contains(binaryVersion);
        }
        #endregion

        #region Available Versions
        private async Task<(bool, string)> RefreshAndLoadAvailableVersions()
        {
            if (HasValidCache())
            {
                return (true, ReadCache());
            }

            var result = await DownloadContent();
            if (result.Item1)
            {
                await StoreData(result.Item2);
            }

            return result;
        }


        [Serializable]
        internal struct Binary
        {
            public string version;
            public string download_url;
            public double url_validity;
        }

        [Serializable]
        internal struct AvailableBinaries
        {
            public Binary[] binaries;
        }
        private bool LoadContentJsonData(string jsonData)
        {
            _availableVersions.Clear();
            try
            {
                var result = JsonUtility.FromJson<AvailableBinaries>(jsonData);
                _availableVersions = result.binaries.ToList();
                _cacheDurationInHours = _availableVersions.Count(bin => bin.url_validity > 0) > 0 ? _availableVersions.FindAll(bin => bin.url_validity > 0).Min(bin => bin.url_validity) : DefaultCacheDurationInHours;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        #region Data Caching

        private bool HasValidCache()
        {
            var lastDownloadTimestamp = SessionState.GetString(_lastDownloadTimestampKey, null);
            if (!File.Exists(CacheFilePath) || string.IsNullOrEmpty(lastDownloadTimestamp))
            {
                return false;
            }

            var lastDownloadTime = DateTime.Parse(lastDownloadTimestamp, CultureInfo.InvariantCulture);
            return DateTime.Now - lastDownloadTime <= TimeSpan.FromHours(_cacheDurationInHours);
        }

        private string ReadCache()
        {
            return File.ReadAllText(CacheFilePath);
        }

        internal async Task StoreData(string jsonData)
        {
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllTextAsync(CacheFilePath, jsonData);
            SessionState.SetString(_lastDownloadTimestampKey, DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }

        private void ClearCache()
        {
            if (File.Exists(CacheFilePath))
            {
                File.Delete(CacheFilePath);
            }

            SessionState.EraseString(_lastDownloadTimestampKey);
        }

        #endregion

        #region Data Download

        internal async Task<(bool, string)> DownloadContent()
        {
            var path = Utils.SystemUtils.GetDownloadURL();
            var client = new HttpClient();
            var cat = Utils.AuthUtils.GetAuthToken();
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (!string.IsNullOrEmpty(cat))
            {
                request.Headers.Add("rl_release_employee_cat", cat);
            }
            try
            {
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentType.MediaType.Contains("json"))
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return (true, result);
                }

                return (false, "");

            }
            catch (Exception)
            {
                // exception is thrown even when the result doesn't have expected format
                return (false, "");
            }
        }
        #endregion
        #endregion

    }

}
