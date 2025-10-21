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

using UnityEngine;
using System.Collections.Concurrent;
using Meta.Voice;
using Meta.WitAi;
using Meta.WitAi.TTS;
using Meta.WitAi.Requests;
using Oculus.Voice;

namespace Oculus.VoiceSDK.Utilities
{
    /// <summary>
    /// The various request types within the voice sdk
    /// </summary>
    public enum VoiceErrorRequestType
    {
        /// <summary>
        /// Audio input transcription, NLP or LLM requests
        /// </summary>
        AudioInputAnalysisRequest,

        /// <summary>
        /// Text input NLP or LLM requests
        /// </summary>
        TextInputAnalysisRequest,

        /// <summary>
        /// Text-to-speech request for playing back audio
        /// </summary>
        TextToSpeechRequest
    }

    /// <summary>
    /// A script added to get services to perform various errors
    /// </summary>
    public class VoiceErrorSimulator : MonoBehaviour
    {
        /// <summary>
        /// Voice services
        /// </summary>
        public VoiceService[] voiceServices;

        /// <summary>
        /// Tts services to subscribe to
        /// </summary>
        public TTSService ttsService;

        /// <summary>
        /// Lookup for requested simulated errors
        /// </summary>
        private ConcurrentDictionary<VoiceErrorRequestType, VoiceErrorSimulationType> _requests = new();

        /// <summary>
        /// On enable, find services
        /// </summary>
        protected virtual void OnEnable()
        {
            RefreshServices();
            SetListeners(true);
        }

        /// <summary>
        /// Obtain services if possible
        /// </summary>
        protected virtual void RefreshServices()
        {
            if (voiceServices == null) voiceServices = gameObject.GetComponentsInChildren<AppVoiceExperience>(true);
            if (ttsService == null) ttsService = gameObject.GetComponentInChildren<TTSService>(true);
        }

        /// <summary>
        /// Adds or removes listeners
        /// </summary>
        private void SetListeners(bool add)
        {
            if (voiceServices == null) return;
            foreach (var voiceService in voiceServices)
            {
                voiceService.VoiceEvents.OnRequestInitialized.SetListener(SimulateVoiceRequestError, add);
            }
        }

        /// <summary>
        /// On disable, remove all listeners
        /// </summary>
        protected virtual void OnDisable()
        {
            SetListeners(false);
        }

        /// <summary>
        /// Simulate an error on this npc
        /// </summary>
        public void SimulateError(VoiceErrorRequestType requestType, VoiceErrorSimulationType simulationType)
        {
            // If text-to-speech, send to tts service
            if (requestType == VoiceErrorRequestType.TextToSpeechRequest)
            {
                ttsService.SimulatedErrorType = simulationType;
                return;
            }

            // Otherwise,
            _requests[requestType] = simulationType;
        }

        /// <summary>
        /// Setup voice request
        /// </summary>
        private void SimulateVoiceRequestError(VoiceServiceRequest request)
        {
            // Ignore external pubsub requests
            if (!request.IsLocalRequest) return;

            // Determine if error should be simulated
            var simulateRequestType = (VoiceErrorRequestType)(-1);
            var simulationErrorType = (VoiceErrorSimulationType)(-1);
            // Simulate ASR error
            if (request.InputType == NLPRequestInputType.Audio && _requests.ContainsKey(VoiceErrorRequestType.AudioInputAnalysisRequest))
            {
                simulateRequestType = VoiceErrorRequestType.AudioInputAnalysisRequest;
                _requests.TryRemove(simulateRequestType, out simulationErrorType);
            }
            // Simulate LLM error
            else if (request.InputType == NLPRequestInputType.Text && _requests.ContainsKey(VoiceErrorRequestType.TextInputAnalysisRequest))
            {
                simulateRequestType = VoiceErrorRequestType.TextInputAnalysisRequest;
                _requests.TryRemove(simulateRequestType, out simulationErrorType);
            }
            if (simulateRequestType == (VoiceErrorRequestType)(-1)
                || simulationErrorType == (VoiceErrorSimulationType)(-1)) return;

            // Simulate Error
            request.SimulateError(simulationErrorType);
        }
    }
}
