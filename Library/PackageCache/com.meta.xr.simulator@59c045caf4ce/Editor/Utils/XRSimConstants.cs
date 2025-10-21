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

namespace Meta.XR.Simulator.Editor
{
    internal static class XRSimConstants
    {

#if UNITY_EDITOR_OSX
        public const string AppId = "9961418137219995";
        public const string ReleaseNotesUrl = "https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-mac-arm";
        public const string UnityXRPackage = "com.unity.xr.openxr@>=1.13.0";
        private static readonly string AppDataFolderPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library");
        public static readonly string TokenGeneratorPath = "/usr/local/bin/rl_releng_tooling/rl_fetch_token.sh";
#else
        public const string AppId = "28549923061320041";
        public const string ReleaseNotesUrl = "https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows";
        public const string UnityXRPackage = "com.unity.xr.openxr";
        private static readonly string AppDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static readonly string TokenGeneratorPath = "C:\\tools\\rl_releng_tooling\\rl_fetch_token.bat";
#endif
        public const string OpenXrRuntimeEnvKey = "XR_RUNTIME_JSON";
        public const string PreviousOpenXrRuntimeEnvKey = "XR_RUNTIME_JSON_PREV";
        public const string OpenXrSelectedRuntimeEnvKey = "XR_SELECTED_RUNTIME_JSON";
        public const string PreviousOpenXrSelectedRuntimeEnvKey = "XR_SELECTED_RUNTIME_JSON_PREV";
        public const string OpenXrOtherRuntimeEnvKey = "OTHER_XR_RUNTIME_JSON";
        public const string XrSimConfigEnvKey = "META_XRSIM_CONFIG_JSON";
        public const string PreviousXrSimConfigEnvKey = "META_XRSIM_CONFIG_JSON_PREV";
        public const string ProjectTelemetryId = "META_PROJECT_TELEMETRY_ID";
        public const string PublicName = "Meta XR Simulator";
        public const string MenuPath = "Meta/" + PublicName;
        public const string PackageName = "com.meta.xr.simulator";

        public const string OculusXRPackageName = "com.unity.xr.oculus";

        private static int? _packageVersion;
        public static int? PackageVersion => _packageVersion ??= Utils.PackageManagerUtils.GetPackageVersion(PackageName);

        public static readonly string XrSimDataPath = Path.Join(AppDataFolderPath, "MetaXR", "MetaXrSimulator");
        public static string XrSimDataFolderPath => Path.Join(XrSimDataPath, Settings.PreferredVersion);
        public static string JsonPath => Path.GetFullPath(Path.Join(XrSimDataFolderPath, "meta_openxr_simulator.json"));
        public static readonly string DownloadFolderPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

}
