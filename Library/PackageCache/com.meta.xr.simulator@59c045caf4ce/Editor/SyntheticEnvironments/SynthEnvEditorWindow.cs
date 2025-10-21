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
using UnityEngine;
using UnityEngine.UIElements;

namespace Meta.XR.Simulator.Editor.SyntheticEnvironments
{
    internal class SynthEnvEditorWindow : EditorWindow
    {
        public static async void ShowWindow()
        {
            bool success = await Installer.EnsureMetaXRSimulatorInstalled();
            if (!success)
            {
                Utils.LogUtils.DisplayDialogOrError("Meta XR Simulator Not Installed",
                                $"Installation failed please report the error.");
                return;
            }

            var window = GetWindow<SynthEnvEditorWindow>();
            window.titleContent = new GUIContent("Environment Servers");
            window.Show();
        }

        public static SynthEnvEditorWindow GetWindow()
        {
            ShowWindow();
            return GetWindow<SynthEnvEditorWindow>();
        }

        public void OnEnable()
        {
            Registry.OnEnvironmentRegistered += RegisterEnvironment;

            Registry.RegisteredEnvironments.ForEach(RegisterEnvironment);
        }

        public void OnDisable()
        {
            Registry.OnEnvironmentRegistered -= RegisterEnvironment;
        }

        private void RegisterEnvironment(SyntheticEnvironment environment)
        {
            var button = new Button();
            button.text = environment.Name;
            button.RegisterCallback<MouseUpEvent>((evt) =>
            {
                environment.Launch(true, Settings.DisplayServers);
            });
            rootVisualElement.Add(button);
        }
    }
}
