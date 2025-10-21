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
using UnityEditor;

namespace Meta.XR.Simulator.Editor.SyntheticEnvironments
{
    internal static class SyntheticEnvironmentServer
    {
        private const string Name = "Synthetic Environment Server";
        public const string MenuPath = XRSimConstants.MenuPath + "/" + Name;
        private const string Port = "33792";

        public static void Start(string environmentName, string binaryPath, string binaryPathWithDot, bool stopExisting, bool createWindow)
        {
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
            using var marker = new OVRTelemetryMarker(XRSimTelemetryConstants.MarkerId.SESInteraction);
            marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.Action, environmentName);
#endif

            Utils.LogUtils.ReportInfo(Name, String.Format("Launching {0}, stopExisting={1}, createWindow={2}", environmentName, stopExisting, createWindow));

#if UNITY_EDITOR_OSX
            if (!Directory.Exists(binaryPath))
#else
            if (!Utils.SystemUtils.FileExists(binaryPath))
#endif
            {
                if (binaryPathWithDot == null ||
#if UNITY_EDITOR_OSX
                    !Directory.Exists(binaryPathWithDot))
#else
                    !Utils.SystemUtils.FileExists(binaryPathWithDot))
#endif
                {
                    Utils.LogUtils.DisplayDialogOrError(Name, "failed to find " + binaryPath + " or " + binaryPathWithDot);

#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.SetResult(OVRPlugin.Qpl.ResultType.Fail);
#endif
                    return;
                }
                binaryPath = binaryPathWithDot;
            }

            var existingProcess = Utils.ProcessUtils.GetProcessesByPort(Port).Count > 0;
            if (existingProcess)
            {
                if (!stopExisting) return;

                Utils.LogUtils.ReportWarning(Name, "A synthetic environment server is already running. Stopping it to start the new server");
                Stop();
            }

            // launch the binary
            Settings.LastEnvironment = environmentName;
            var hidden = (createWindow ? "" : "-batchmode");
#if UNITY_EDITOR_OSX
            var arguments = new string[]{binaryPath, "--args", environmentName, hidden};
            var args = Utils.EscapeArguments(arguments);
            Utils.ProcessUtils.LaunchProcess("open", args, Name, createWindow);
#else
            var arguments = new string[] { environmentName, hidden };
            Utils.ProcessUtils.LaunchProcess(binaryPath, String.Join(" ", arguments), Name, createWindow);
#endif

        }

        [MenuItem(SyntheticEnvironmentServer.MenuPath + "/Start Environment Server")]
        public static void ShowWindow()
        {
            SynthEnvEditorWindow.ShowWindow();
        }

        [MenuItem(MenuPath + "/Stop Environment Server")]
        public static void Stop()
        {
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
            using var marker = new OVRTelemetryMarker(XRSimTelemetryConstants.MarkerId.SESInteraction);
            marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.Action, "stop");
#endif

            try
            {
                Utils.ProcessUtils.StopProcess(Port, Name);
            }
            catch (Exception e)
            {
                // NOTE: just report the error
                Utils.LogUtils.ReportWarning(Name, e.Message);
            }

            try
            {
                // This will also stop the LocalSharingServer
                LocalSharingServer.Stop();
            }
            catch (Exception e)
            {
                // NOTE: just report the error
                Utils.LogUtils.ReportWarning(Name, e.Message);
            }
        }
    }
}
