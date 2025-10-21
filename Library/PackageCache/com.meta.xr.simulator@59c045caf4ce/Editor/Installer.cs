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
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.Networking;

namespace Meta.XR.Simulator.Editor
{
    internal static class Installer
    {
        private const string Name = "Meta XR Simulator Installer";
        public static bool IsInstalled => Directory.Exists(XRSimConstants.XrSimDataFolderPath);
        public static event Action OnInstalled;

        private static UnityWebRequest _webRequest = null;

#if UNITY_EDITOR_OSX
        private const string DLLName = "SIMULATOR.so";
#else
        private const string DLLName = "SIMULATOR.dll";
#endif
        private static string DllPath => Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath, DLLName));

        public static bool IsSimulatorInstalled()
        {
            return !string.IsNullOrEmpty(XRSimConstants.JsonPath) &&
                    !string.IsNullOrEmpty(DllPath) &&
                    Utils.SystemUtils.FileExists(XRSimConstants.JsonPath) &&
                    Utils.SystemUtils.FileExists(DllPath);
        }

        public static async Task<bool> EnsureMetaXRSimulatorInstalled()
        {

            if (IsSimulatorInstalled())
            {
                return true;
            }

            var binary = await Utils.VersionUtils.TryToLatestAvailableVersion();

            if (binary == null || Utils.VersionUtils.IsVersionInstalled(binary?.version))
            {
                return false;
            }

            return await InstallXRSimulator(binary?.download_url, binary?.version);
        }

        internal static async Task<bool> InstallXRSimulator(string downloadUrl, string version)
        {
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
            var marker = new OVRTelemetryMarker(XRSimTelemetryConstants.MarkerId.BinariesInstalled);
            marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.XRSimVersion, version);
#endif
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            return await InstallXRSim(version, downloadUrl, () =>
            {
                Settings.PreferredVersion = version;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                OnInstalled?.Invoke();
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                marker.SetResult(OVRPlugin.Qpl.ResultType.Success);
#endif
            }, errorMessage =>
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                Utils.LogUtils.DisplayDialogOrError(Name, errorMessage, true);
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                marker.SetResult(OVRPlugin.Qpl.ResultType.Fail);
#endif
            });
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Cancel entering play mode
                UnityEngine.Debug.LogError($"Synthetic Environments installation is in progress. Please wait until is finished.");
                EditorApplication.isPlaying = false;
            }
        }

        private static async Task<bool> InstallXRSim(string version, string downloadUrl, Action onSuccess, Action<string> onError)
        {
            var downloadedZipPath =
                            Path.Combine(XRSimConstants.DownloadFolderPath, $"meta_xr_simulator_{version}.zip");
            var installDir = Path.Combine(XRSimConstants.XrSimDataPath, version);
            if (!await DownloadPackage(downloadedZipPath, downloadUrl, errorMessage =>
                {
                    onError?.Invoke(errorMessage);
                }))
            {
                return false;
            }

            if (!await Task.Run(() => UnzipPackage(downloadedZipPath, installDir, s =>
                {
                    onError?.Invoke(s);
                })))
            {
                return false;
            }

            Utils.LogUtils.ReportInfo(Name, "finished extracting " + downloadedZipPath + " to " + installDir);
            onSuccess?.Invoke();
            return true;
        }

        internal static async Task<bool> DownloadPackage(string downloadPath, string url, Action<string> onError)
        {
            // check if file exists before downloading it again
            if (Utils.SystemUtils.FileExists(downloadPath))
            {
                Utils.LogUtils.ReportInfo(Name, "Found " + downloadPath + ", skipping download");
                return true;
            }

            int progressId = Utils.LogUtils.CreateProgress(Name, false);

            _webRequest = UnityWebRequest.Get(url);
            var handler = new DownloadHandlerFile(downloadPath);
            _webRequest.downloadHandler = handler;
            handler.removeFileOnAbort = true;

            UnityWebRequestAsyncOperation operation = _webRequest.SendWebRequest();
            operation.completed += _ =>
            {
                Utils.LogUtils.ReportInfo(Name, "finished downloading " + url);
            };

            while (!_webRequest.downloadHandler.isDone && !operation.isDone)
            {
                Progress.Report(progressId, _webRequest.downloadProgress, "Downloading package");
                await Task.Yield();
            }

            if (_webRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(_webRequest.error);
                Progress.Finish(progressId, Progress.Status.Failed);
                return false;
            }

            Utils.LogUtils.ReportInfo(Name, "Finished saving data to " + downloadPath);
            _webRequest = null;
            Progress.Remove(progressId);

#if UNITY_EDITOR_OSX
            // Remove quarantine attribute from downloaded file
            {
                const string Attribute = "com.apple.provenance";
                var args = Utils.EscapeArguments(new string[] { "-d", Attribute, downloadPath });
                var (retCode, contents) = Utils.ProcessUtils.ExecuteProcess("xattr", args);
                if(retCode != 0)
                {
                    Utils.LogUtils.ReportError(Name, string.Format("failed to remove {0}, retCode={1}, contents={2}", Attribute, retCode, contents));
                }
            }
#endif
            return true;
        }

        internal static bool UnzipPackage(string downloadPath, string installDir, Action<string> onError)
        {
            // unzip
            ZipArchive archive;
            try
            {
                archive = ZipFile.OpenRead(downloadPath);
            }
            catch (Exception e)
            {
                onError?.Invoke(e.Message);
                return false;
            }

            // ensure normalized path
            if (!installDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                installDir += Path.DirectorySeparatorChar;
            }

            if (Directory.Exists(installDir))
            {
                // Ensure directory is deleted before extracting
                Directory.Delete(installDir, true);
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("/"))
                {
                    continue;
                }

                // Gets the full path to ensure that relative segments are removed.
                string destinationPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));

                // Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
                // are case-insensitive.
                if (!destinationPath.StartsWith(installDir, StringComparison.Ordinal))
                {
                    continue;
                }
                // create directory if it doesn't exist
                var parentDir = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                entry.ExtractToFile(destinationPath, true);

#if UNITY_EDITOR_OSX
                // Update the file attributes for file
                string subString = "";
                int extendedAttributes = entry.ExternalAttributes >> 16;
                if(extendedAttributes != 0) {
                    string attrString = Convert.ToString(extendedAttributes, 8);
                    subString = attrString.Substring(attrString.Length - 4);
                } else if(entry.FullName.Contains("SyntheticEnvironmentServer")) {
                    // This is a hack to set the executable bit on the SyntheticEnvironmentServer binary
                    subString = "750";
                }

                if(subString.Length > 0) {
                    Utils.LogUtils.ReportInfo(Name, "setting permissions on " + destinationPath + " to " + subString);
                    var args = Utils.EscapeArguments(new string[] { subString, destinationPath });
                    var (retCode, contents) = Utils.ProcessUtils.ExecuteProcess("chmod", args);
                    if(retCode != 0)
                    {
                        Utils.LogUtils.ReportError(Name, "failed to set permissions on " + destinationPath + ", retCode:" + retCode + ", contents:" + contents);
                    }
                }
#endif
            }

            archive.Dispose();
            return true;
        }
    }
}
