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
    internal static class JsonServer
    {
        private const string Name = "JSON Server";
        private const string MenuPath = XRSimConstants.MenuPath + "/" + Name;
        private const string Port = "33792";

#if UNITY_EDITOR_OSX
        private static string FullPath =>
            Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath,"json_synthenv_server~","json_server"));
#else
        private static string FullPath =>
            Path.GetFullPath(Path.Join(XRSimConstants.XrSimDataFolderPath, "json_synthenv_server~", "json_server.exe"));
#endif

        [MenuItem(MenuPath + "/Launch JSON Server")]
        private static void StartFromMenu()
        {
            Start(EditorUtility.OpenFilePanel("Open File", "", "json"));
        }

        public static void Start(string jsonPath)
        {
            Utils.LogUtils.ReportInfo(Name, "Launching JSON server");

            var binaryPath = FullPath;
            if (!Utils.SystemUtils.FileExists(binaryPath))
            {
                Utils.LogUtils.DisplayDialogOrError(Name, "failed to find " + binaryPath);
                return;
            }

            if (!Utils.SystemUtils.FileExists(jsonPath))
            {
                if (jsonPath != "")
                {
                    Utils.LogUtils.DisplayDialogOrError(Name, "failed to find " + jsonPath);
                }
                return;
            }

            // Always force restart JsonServers
            if (Utils.ProcessUtils.GetProcessesByPort(Port).Count > 0)
            {
                Stop();
            }

            // launch the binary
            Utils.ProcessUtils.LaunchProcess(binaryPath, jsonPath, Name);
        }

        [MenuItem(MenuPath + "/Stop JSON Server")]
        public static void Stop()
        {
            Utils.ProcessUtils.StopProcess(Port, Name);
        }
    }
}
