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
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if META_XR_SDK_CORE_78_OR_NEWER
using Meta.XR.Editor.Id;
using Meta.XR.Editor.Notifications;
using Meta.XR.Editor.UserInterface;
#endif

namespace Meta.XR.Simulator.Editor
{
    [InitializeOnLoad]
    internal static class Updater
    {
        private static readonly string NextTriggerTimeKey = XRSimConstants.PackageName + ".next_trigger_time_key";
        private static DateTime _nextTriggerTime;
        private const int TimeIntervalSeconds = 24 * 60 * 60; // 24 hours

        private static string SavedTicks
        {
            get => EditorPrefs.GetString(NextTriggerTimeKey, DateTime.UtcNow.Ticks.ToString());
            set => EditorPrefs.SetString(NextTriggerTimeKey, value);
        }

        private static readonly string UpdateAvailableKey = XRSimConstants.PackageName + ".update_available_key";
        public static bool UpdateAvailable
        {
            get => EditorPrefs.GetBool(UpdateAvailableKey, false);
            private set => EditorPrefs.SetBool(UpdateAvailableKey, value);
        }


        static Updater()
        {
            _nextTriggerTime = new(Convert.ToInt64(SavedTicks));
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static async void OnFocusChanged(bool focus)
        {
            if (EditorApplication.isPlaying)
            {
                // Currently we are in play mode. Wait next event
                return;
            }

            var utcNow = DateTime.UtcNow;

            // Check if the current time is past the next trigger time
            if (utcNow < _nextTriggerTime)
            {
                return;
            }

            if (!await CheckForUpdate())
            {
                // check again on next refocus
                return;
            }

            // Update next trigger time
            _nextTriggerTime = utcNow.AddSeconds(TimeIntervalSeconds);
            SavedTicks = _nextTriggerTime.Ticks.ToString();

            if (!UpdateAvailable)
            {
                return;
            }

            if (Settings.NotifyIfNewVersionsIsAvailable)
            {
                ShowUpdateNotice();
            }
        }

        [MenuItem(XRSimConstants.MenuPath + "/Check For Updates")]
        public static async void TriggerUpdateCheckMenu()
        {
            if (!await CheckForUpdate(refresh: true))
            {
                return;
            }

            // Update next trigger time
            _nextTriggerTime = DateTime.UtcNow.AddSeconds(TimeIntervalSeconds);
            SavedTicks = _nextTriggerTime.Ticks.ToString();

            if (UpdateAvailable)
            {
                ShowUpdateNotice();
            }
        }

        public static async Task<bool> CheckForUpdate(bool refresh = false)
        {
            var availableVersion = await Utils.VersionUtils.TryToLatestAvailableVersion(refresh);
            var maxInstalledVersion = Utils.GetMaxVersion(Utils.VersionUtils.TryToGetInstalledVersions());

            if (availableVersion == null || Utils.CompareVersions(availableVersion?.version, maxInstalledVersion) <= 0)
            {
                // There is no new version available
                UpdateAvailable = false;
                return true;
            }

            if (EditorApplication.isPlaying)
            {
                // Just retry on next refresh
                return false;
            }

            UpdateAvailable = true;
            return true;
        }

        public static async Task<bool> TriggerUpdate()
        {
            var availableVersion = await Utils.VersionUtils.TryToLatestAvailableVersion();
            var maxInstalledVersion = Utils.GetMaxVersion(Utils.VersionUtils.TryToGetInstalledVersions());

            if (availableVersion == null || Utils.CompareVersions(availableVersion?.version, maxInstalledVersion) <= 0)
            {
                // There is no new version available
                return true;
            }

            var simulatorActivated = Enabler.Activated;
            if (simulatorActivated)
            {
                Utils.XRSimUtils.DeactivateSimulator(false, Origin.Updater);
            }

            if (await Installer.InstallXRSimulator(availableVersion?.download_url, availableVersion?.version))
            {
                UpdateAvailable = false;
                if (simulatorActivated)
                {
                    await Utils.XRSimUtils.ActivateSimulator(false, Origin.Updater);
                }

                Utils.LogUtils.ReportInfo("Meta XR Simulator", $"Meta XR Simulator is updated to version {availableVersion?.version}");
                return true;
            }

            return false;
        }

        private static void ShowUpdateNotice()
        {
#if META_XR_SDK_CORE_78_OR_NEWER
            var notification = new Notification("MetaXRSimulatorUpdateNotice")
            {
                Icon = Meta.XR.Editor.PlayCompanion.Styles.Contents.MetaXRSimulator,
                Duration = 20f,
                ShowCloseButton = true,
                GradientColor = Meta.XR.Editor.UserInterface.Utils.HexToColor("#6a6a6a")
            };

            var closeAction = new Action(() => { notification.Remove(Origins.Self); });
            notification.Items = new IUserInterfaceItem[]
                            { new GroupedItem(GetItems(closeAction), XR.Editor.UserInterface.Utils.UIItemPlacementType.Vertical) };

            notification.Enqueue(Origins.Console);
#else

            var update = Utils.LogUtils.DisplayDialog("Meta XR Simulator", "Meta XR Simulator Update Available. New version includes improvements and bug fixes.", "Update", "More details");
            if (update)
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                TriggerUpdate();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            else
            {
                SettingsService.OpenUserPreferences(Settings.SettingsPath);
            }
#endif
        }

#if META_XR_SDK_CORE_78_OR_NEWER
        private static IEnumerable<IUserInterfaceItem> GetItems(Action closeAction)
        {
            yield return new Label("Meta XR Simulator Update Available", UIStyles.GUIStyles.Title);
            yield return new AddSpace();
            yield return new BulletedLabel("New version includes improvements and bug fixes.");
            yield return new AddSpace();

            yield return new GroupedItem(GetNotificationButtons(closeAction));
        }

        private static IEnumerable<IUserInterfaceItem> GetNotificationButtons(Action closeAction)
        {
            yield return new AddSpace(flexibleSpace: true);

            var buttonLayout = new[] { GUILayout.Height(20f), GUILayout.Width(90f) };
            var highlightedButtonColor = XR.Editor.UserInterface.Styles.Colors.LightMeta;

            yield return new Button(
                            new ActionLinkDescription
                            {
                                Content = new GUIContent("More Details"),
                                Action = () =>
                                {
                                    SettingsService.OpenUserPreferences(Settings.SettingsPath);
                                    closeAction();
                                }
                            },
                            buttonLayout);

            yield return new Button(new ActionLinkDescription
            {
                Content = new GUIContent("Update"),
                Action = async () =>
                {
                    closeAction();
                    await TriggerUpdate();
                },
                BackgroundColor = highlightedButtonColor,
                ActionData = null,
                Id = "UpdateMetaXRSimulator"
            }, buttonLayout);

        }
#endif
    }
}
