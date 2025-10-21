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
using System.Linq;
using UnityEngine;
using UnityEditor;

using Meta.XR.Simulator.Editor.SyntheticEnvironments;

namespace Meta.XR.Simulator.Editor
{
    internal class Settings : SettingsProvider
    {
        public static string SettingsPath => $"Preferences/Meta XR/{XRSimConstants.PublicName}";
        private const string LastEnvironmentKey = XRSimConstants.PackageName + ".lastEnvironment_key";

        public Settings(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => new Settings(SettingsPath, SettingsScope.User);

        internal static string LastEnvironment
        {
            get => EditorPrefs.GetString(LastEnvironmentKey, "LivingRoom");
            set => EditorPrefs.SetString(LastEnvironmentKey, value);
        }

        private const string AutomaticServersKey = XRSimConstants.PackageName + ".automaticservers_key";

        internal static bool AutomaticServers
        {
            get => EditorPrefs.GetBool(AutomaticServersKey, true);
            set => EditorPrefs.SetBool(AutomaticServersKey, value);
        }

        private const string DisplayServersKey = XRSimConstants.PackageName + ".displayservers_key";
        internal static bool DisplayServers
        {
            get => EditorPrefs.GetBool(DisplayServersKey, false);
            private set => EditorPrefs.SetBool(DisplayServersKey, value);
        }

        private const string PreferredVersionKey = XRSimConstants.PackageName + ".preferredversionkey";

        internal static string PreferredVersion
        {
            get
            {
                var value = EditorPrefs.GetString(PreferredVersionKey, "");
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                var maxInstalledVersion = Utils.GetMaxVersion(Utils.VersionUtils.TryToGetInstalledVersions());

                if (string.IsNullOrEmpty(maxInstalledVersion))
                {
                    return XRSimConstants.PackageVersion.ToString();
                }

                return maxInstalledVersion;
            }

            set
            {
                if (Utils.XRSimUtils.IsSimulatorActivated())
                {
                    Utils.XRSimUtils.DeactivateSimulator(false, Origin.Settings);
                }

                EditorPrefs.SetString(PreferredVersionKey, value);
                Registry.RefreshRegistryOnVersionChange();
            }
        }

        private const string NotifyIfNewVersionIsAvailable = XRSimConstants.PackageName + ".newversionisavailable";

        internal static bool NotifyIfNewVersionsIsAvailable
        {
            get => EditorPrefs.GetBool(NotifyIfNewVersionIsAvailable, true);
            private set => EditorPrefs.SetBool(NotifyIfNewVersionIsAvailable, value);
        }

        public override void OnGUI(string searchContext)
        {
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
            var marker = new OVRTelemetryMarker(XRSimTelemetryConstants.MarkerId.SettingsChange);
#endif
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 256.0f;

            EditorGUILayout.Space(10.0f);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Updater.CheckForUpdate();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            if (Updater.UpdateAvailable)
            {
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.PendingUpdate, true);
#endif
                Styles.DrawUpdateNotice(() =>
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Updater.TriggerUpdate();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                });
            }

            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            {
                // Enable Toggle
                EditorGUI.BeginChangeCheck();
                var enable = EditorGUILayout.Toggle(new GUIContent("Enable", "Set Play mode to use Meta XR Simulator"),
                                Enabler.Activated);
                if (EditorGUI.EndChangeCheck())
                {

                    if (enable)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Utils.XRSimUtils.ActivateSimulator(false, Origin.Settings);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    else
                    {
                        Utils.XRSimUtils.DeactivateSimulator(false, Origin.Settings);
                    }
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.XRSimEnabled, enable);
#endif
                }
            }

            {
                if (Utils.SystemUtils.DirectoryExists(XRSimConstants.XrSimDataPath))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Installation Directory", GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (EditorGUILayout.LinkButton(XRSimConstants.XrSimDataPath))
                    {
                        Utils.SystemUtils.OpenDirectory(XRSimConstants.XrSimDataPath);
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                        marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.OpenInstallationDir, true);
#endif
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Versions", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            {
                // Notify when new version is available
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.Toggle(
                                new GUIContent("Notify When New Version Is Available",
                                                "Notifies when new version of Meta XR Simulator is available."),
                                NotifyIfNewVersionsIsAvailable);
                if (EditorGUI.EndChangeCheck())
                {
                    NotifyIfNewVersionsIsAvailable = value;
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.NotifyNewVersionAvailable, value);
#endif
                }
            }

            {
                // Existing versions
                var installedVersions = Utils.VersionUtils.TryToGetInstalledVersions(refresh: true);
                var selectedIndex = Array.IndexOf(installedVersions, PreferredVersion);
                if (selectedIndex == -1)
                {
                    PreferredVersion = Utils.GetMaxVersion(installedVersions);
                    selectedIndex = Array.IndexOf(installedVersions, PreferredVersion);
                }

                EditorGUI.BeginChangeCheck();
                var index = EditorGUILayout.Popup("Selected Version", selectedIndex, installedVersions);
                if (EditorGUI.EndChangeCheck())
                {
                    PreferredVersion = installedVersions[index];
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.PreferredVersion, PreferredVersion);
#endif
                }

            }

            {
                // Available versions
                var availableVersions = Utils.VersionUtils.GetAvailableVersionsFromCache();
                if (availableVersions.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    var index = EditorGUILayout.Popup("Available Versions", -1, availableVersions.Select(binary => binary.version).ToArray());
                    if (EditorGUI.EndChangeCheck())
                    {
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                        marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.TriggerInstall, availableVersions[index].version);
#endif
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Installer.InstallXRSimulator(availableVersions[index].download_url, availableVersions[index].version);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Servers", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            {
                Registry.RefreshNames();
                // Automatic Synthetic Environment Toggle
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.Toggle(
                    new GUIContent("Automatically Start/Stop Servers",
                        "Whether or not the Synthetic Environment Servers and the Local Sharing Servers are started and stopped when entering and exiting Play Mode."),
                    AutomaticServers);
                if (EditorGUI.EndChangeCheck())
                {
                    AutomaticServers = value;
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.AutomaticServers, value);
#endif
                }
            }

            // Servers section
            {
                // Display Synthetic Environment Window
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.Toggle(
                    new GUIContent("Display Servers",
                        "Whether or not the Synthetic Environment Server and the Local Sharing Server are displayed or hidden."),
                    DisplayServers);
                if (EditorGUI.EndChangeCheck())
                {
                    DisplayServers = value;
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.DisplayServers, value);
#endif
                }
            }

            {
                // Preferred Environment Dropdown
                Registry.RefreshNames();
                var selectedRoom = Registry.GetByInternalName(LastEnvironment);
                var selectedIndex = selectedRoom?.Index ?? 0;
                EditorGUI.BeginChangeCheck();
                var index = EditorGUILayout.Popup("Selected Environment", selectedIndex, Registry.Names);
                if (EditorGUI.EndChangeCheck())
                {
                    selectedRoom = Registry.GetByIndex(index);
                    LastEnvironment = selectedRoom.InternalName;
#if META_XR_SDK_CORE_SUPPORTS_TELEMETRY
                    marker.AddAnnotation(XRSimTelemetryConstants.AnnotationType.SelectedEnvironment, LastEnvironment);
#endif
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }
    }
}
