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

using UnityEditor;
using Menu = UnityEditor.Menu;
using System.Threading.Tasks;

namespace Meta.XR.Simulator.Editor
{
    /// <summary>
    /// Static Helper that controls activation and deactivation of Meta XR Simulator.
    /// </summary>
    /// <seealso cref="Activated"/>
    /// <seealso cref="ActivateSimulatorAsync"/>
    /// <seealso cref="DeactivateSimulator"/>
    /// <seealso cref="ToggleSimulator"/>
    [InitializeOnLoad]
    public static class Enabler
    {
        private const string ActivateSimulatorMenuPath = XRSimConstants.MenuPath + "/Activate";
        private const string DeactivateSimulatorMenuPath = XRSimConstants.MenuPath + "/Deactivate";

        /// <summary>
        /// Whether Meta XR Simulator is Activated.
        /// </summary>
        public static bool Activated => Installer.IsSimulatorInstalled() && Utils.XRSimUtils.IsSimulatorActivated();

        static Enabler()
        {
            Utils.XRSimUtils.SetOpenXROtherVariable();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Utils.XRSimUtils.VerifyAndCorrectActivation();
            }
        }

        [MenuItem(ActivateSimulatorMenuPath, true, 0)]
        private static bool ValidateSimulatorActivated()
        {
            Menu.SetChecked(ActivateSimulatorMenuPath, Activated);
            return true;
        }

        [MenuItem(ActivateSimulatorMenuPath, false, 0)]
        private static async Task ActivateSimulatorMenuItem()
        {
            await Utils.XRSimUtils.ActivateSimulator(false, Origin.Menu);
        }

        [MenuItem(DeactivateSimulatorMenuPath, true, 1)]
        private static bool ValidateSimulatorDeactivated()
        {
            Menu.SetChecked(DeactivateSimulatorMenuPath, !Activated);
            return true;
        }

        [MenuItem(DeactivateSimulatorMenuPath, false, 1)]
        private static void DeactivateSimulatorMenuItem()
        {
            Utils.XRSimUtils.DeactivateSimulator(false, Origin.Menu);
        }

        /// <summary>
        /// Activates Meta XR Simulator
        /// </summary>
        /// <param name="forceHideDialog">Forces any error dialog triggered by this method to be hidden.</param>
        public static void ActivateSimulator(bool forceHideDialog)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            ActivateSimulatorAsync(forceHideDialog);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        /// <summary>
        /// Activates Meta XR Simulator
        /// </summary>
        /// <param name="forceHideDialog">Forces any error dialog triggered by this method to be hidden.</param>
        public static async Task ActivateSimulatorAsync(bool forceHideDialog)
        {
            await Utils.XRSimUtils.ActivateSimulator(forceHideDialog, Origin.Unknown);
        }

        /// <summary>
        /// Deactivates Meta XR Simulator
        /// </summary>
        /// <param name="forceHideDialog">Forces any error dialog triggered by this method to be hidden.</param>
        public static void DeactivateSimulator(bool forceHideDialog)
        {
            Utils.XRSimUtils.DeactivateSimulator(forceHideDialog, Origin.Unknown);
        }

    }
}
