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

using Meta.WitAi.Requests;
using UnityEditor;
using UnityEngine;

namespace Oculus.VoiceSDK.Utilities
{
    [CustomEditor(typeof(VoiceErrorSimulator))]
    public class VoiceErrorSimulatorInspector : Editor
    {
        private VoiceErrorRequestType _requestType = (VoiceErrorRequestType)(-1);
        private VoiceErrorSimulationType _simulatedErrorType = (VoiceErrorSimulationType)(-1);

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var errorSimulator = target as VoiceErrorSimulator;
            if (errorSimulator == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Error Simulation");
            _requestType = (VoiceErrorRequestType)EditorGUILayout.EnumPopup("Request Type", _requestType);
            _simulatedErrorType = (VoiceErrorSimulationType)EditorGUILayout.EnumPopup("Error Type", _simulatedErrorType);
            GUI.enabled = _requestType != (VoiceErrorRequestType)(-1) && _simulatedErrorType != (VoiceErrorSimulationType)(-1);
            if (GUILayout.Button("Simulate Error"))
            {
                errorSimulator.SimulateError(_requestType, _simulatedErrorType);
                _requestType = (VoiceErrorRequestType)(-1);
                _simulatedErrorType = (VoiceErrorSimulationType)(-1);
            }
            GUI.enabled = true;
        }
    }
}
