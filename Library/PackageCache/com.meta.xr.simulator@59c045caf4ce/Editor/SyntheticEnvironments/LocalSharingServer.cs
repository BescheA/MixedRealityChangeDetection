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

using System.IO;
using UnityEditor;

namespace Meta.XR.Simulator.Editor.SyntheticEnvironments
{
    internal static class LocalSharingServer
    {
        private const string Name = "Local Sharing Server";
        private const string MenuPath = XRSimConstants.MenuPath + "/" + Name;
        private const string Port = "33793";

#if UNITY_EDITOR_OSX
        private static string FullPath =>
            Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath ,"local_sharing_server~","local_sharing_server"));
        private static string FullPathWithDot =>
            Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath ,".local_sharing_server","local_sharing_server"));
#else
        private static string FullPath =>
            Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath, "local_sharing_server~", "local_sharing_server.exe"));
        private static string FullPathWithDot =>
            Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath, ".local_sharing_server", "local_sharing_server.exe"));
#endif
        [MenuItem(MenuPath + "/Launch Sharing Server")]
        private static void StartFromMenu()
        {
            Start(true, true);
        }

        public static void Start(bool stopExisting, bool createWindow)
        {
            Utils.LogUtils.ReportInfo(Name, "Launching local sharing server");

            var binaryPath = FullPath;
            if (!Utils.SystemUtils.FileExists(binaryPath))
            {
                Utils.LogUtils.ReportWarning(Name, "failed to find " + binaryPath);
                // NOTE: this is a deprecated path, but we still need to support it for existing users
                binaryPath = FullPathWithDot;
                if (!Utils.SystemUtils.FileExists(binaryPath))
                {
                    Utils.LogUtils.DisplayDialogOrError(Name, "failed to find " + binaryPath);
                    return;
                }
            }

            var existingProcess = Utils.ProcessUtils.GetProcessesByPort(Port).Count > 0;
            if (existingProcess)
            {
                if (!stopExisting) return;

                Stop();
            }

            // launch the binary
            Utils.ProcessUtils.LaunchProcess(binaryPath, "", Name, createWindow);
        }

        [MenuItem(MenuPath + "/Stop Sharing Server")]
        public static void Stop()
        {
            Utils.ProcessUtils.StopProcess(Port, Name);
        }
    }
}
