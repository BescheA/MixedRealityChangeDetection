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
using System.Diagnostics;
using Meta.XR.Simulator.Editor;
using UnityEditor;

using System.Text.RegularExpressions;
using System.Text;
using Utils = Meta.XR.Simulator.Editor.Utils;

#if META_XR_SDK_CORE_SUPPORTS_TOOLBAR
using Meta.XR.Editor.PlayCompanion;
using Meta.XR.Editor.StatusMenu;
using Styles = Meta.XR.Editor.PlayCompanion.Styles;
using Meta.XR.Simulator.Editor.SyntheticEnvironments;

#if META_XR_SDK_CORE_74_OR_NEWER
using Meta.XR.Editor.ToolingSupport;
#endif

#endif

namespace Meta.XR.Simulator
{
    [InitializeOnLoad]
    internal static class ToolbarItem
    {
        private const string ToolbarItemTooltip =
#if UNITY_2022_2_OR_NEWER
            "Set Play mode to use Meta XR Simulator\n<i>Simulates Meta Quest headset and features on desktop</i>";
#else
            "Set Play mode to use Meta XR Simulator\nSimulates Meta Quest headset and features on desktop";
#endif

#if META_XR_SDK_CORE_SUPPORTS_TOOLBAR
#if META_XR_SDK_CORE_74_OR_NEWER
        internal static readonly ToolDescriptor ToolDescriptor = new()
#else
        internal static readonly Meta.XR.Editor.StatusMenu.Item ToolDescriptor = new()
#endif
        {
            Name = XRSimConstants.PublicName,
#if META_XR_SDK_CORE_77_OR_NEWER
            MenuDescription = "Iterate faster in Editor",
#endif
            Color = Meta.XR.Editor.UserInterface.Styles.Colors.Meta,
            Icon = Styles.Contents.MetaXRSimulator,
#if META_XR_SDK_CORE_74_OR_NEWER
            MqdhCategoryId = "857564592791179",
            AddToStatusMenu = true,
            AddToMenu = false,
#endif
#if META_XR_SDK_CORE_68_OR_NEWER
            PillIcon = () =>
                Utils.XRSimUtils.IsSimulatorActivated()
                    ? (Meta.XR.Editor.UserInterface.Styles.Contents.CheckIcon,
                        Meta.XR.Editor.UserInterface.Styles.Colors.Meta,
                        false)
                    : (null, null, false),
#else
                PillIcon = () =>
                    Utils.XRSimUtils.IsSimulatorActivated()
                        ? (Meta.XR.Editor.UserInterface.Styles.Contents.CheckIcon,
                            Meta.XR.Editor.UserInterface.Styles.Colors.Meta)
                        : (null, null),
#endif
            InfoTextDelegate = () => Utils.XRSimUtils.IsSimulatorActivated() ?
                ("Enabled", Meta.XR.Editor.UserInterface.Styles.Colors.SuccessColor) :
#if META_XR_SDK_CORE_77_OR_NEWER
                ("Disabled", Meta.XR.Editor.UserInterface.Styles.Colors.DisabledColor) ,
#else
                ("Disabled", Meta.XR.Editor.UserInterface.Styles.Colors.LightGray) ,
#endif
            OnClickDelegate = async origin => await Utils.XRSimUtils.ToggleSimulator(true, origin.ToString().ToSimulatorOrigin()),
            Order = 10,
            CloseOnClick = false
        };
#endif

        static ToolbarItem()
        {

#if META_XR_SDK_CORE_SUPPORTS_TOOLBAR
#if !META_XR_SDK_CORE_74_OR_NEWER
            StatusMenu.RegisterItem(ToolDescriptor);
#endif

            void MaybeStopServers()
            {
                if (Settings.AutomaticServers)
                {
                    SyntheticEnvironmentServer.Stop();
                }
            }

            var xrSimulatorItem = new Meta.XR.Editor.PlayCompanion.Item()
            {
                Order = 10,
                Name = XRSimConstants.PublicName,
                Tooltip = ToolbarItemTooltip,
                Icon = Styles.Contents.MetaXRSimulator,
                Color = Meta.XR.Editor.UserInterface.Styles.Colors.Meta,
                Show = true,
                ShouldBeSelected = () => Utils.XRSimUtils.IsSimulatorActivated(),
                ShouldBeUnselected = () => !Utils.XRSimUtils.IsSimulatorActivated(),
                OnSelect = async () => { await Utils.XRSimUtils.ActivateSimulator(true, Origin.Toolbar); },
                OnUnselect = () =>
                {
                    Utils.XRSimUtils.DeactivateSimulator(true, Origin.Toolbar);
                    MaybeStopServers();
                },
                OnEnteringPlayMode = () =>
                {
                    if (Settings.AutomaticServers)
                    {
                        Registry.GetByInternalName(Settings.LastEnvironment)?
                            .Launch(true, Settings.DisplayServers);
                    }
                },
                OnExitingPlayMode = MaybeStopServers,
#if META_XR_SDK_CORE_69_OR_NEWER
                OnEditorQuitting = MaybeStopServers,
#endif
            };

            Manager.RegisterItem(xrSimulatorItem);
#endif
        }
    }
}
