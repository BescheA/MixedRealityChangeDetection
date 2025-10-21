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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice;
using Meta.Voice.Logging;
using UnityEngine;
using Meta.WitAi.Attributes;
using Meta.WitAi.Composer.Data;
using Meta.WitAi.Composer.Integrations;
using Meta.WitAi.Composer.Interfaces;
using Meta.WitAi.Configuration;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using UnityEngine.Serialization;

namespace Meta.WitAi.Composer
{
    public abstract class ComposerService : MonoBehaviour, ILogSource
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; }  = LoggerRegistry.Instance.GetLogger(LogCategory.Composer);

        #region VARIABLES
        /// <summary>
        /// All sessions currently active on the composer service and their corresponding ids
        /// </summary>
        public ConcurrentDictionary<string, IComposerSession> ActiveSessions { get; } = new ();

        /// <summary>
        /// Session id most recently used used
        /// </summary>
        public string LastSessionId { get; private set; }

        /// <summary>
        /// The current context map being used with the composer service
        /// </summary>
        public ComposerContextMap CurrentContextMap { get; } = new ();

        /// <summary>
        /// The voice service this composer will use for activation
        /// </summary>
        [Header("Voice Settings")]
        [SerializeField] private VoiceService _voiceService;

        /// <summary>
        /// Whether or not to send all voice service requests through composer.  If disabled, composer will only send
        /// requests made directly from composer.
        /// </summary>
        [Tooltip("Whether or not to send all voice service requests through composer.  If disabled, composer will only send requests made directly from composer.")]
        [FormerlySerializedAs("RouteVoiceServiceToComposer")] [SerializeField]
        private bool _routeVoiceServiceToComposer = true;

        /// <summary>
        /// Whether the composer service will be used for voice activation
        /// </summary>
        public bool RouteVoiceServiceToComposer
        {
            get => _routeVoiceServiceToComposer;
            set
            {
                _routeVoiceServiceToComposer = value;
                Events.OnComposerActiveChange?.Invoke(this, value);
            }
        }

        [Tooltip("Whether or not to end the previous session when starting a new one")]
        public bool EndLastSessionOnStart = true;

        /// <summary>
        /// Whether or not partial tts responses should be sent to attached speech handlers
        /// </summary>
        [Header("Tts Settings")]
        [Tooltip("Whether or not partial tts responses should be sent to attached speech handlers")]
        [FormerlySerializedAs("_handlePartialTts")]
        [SerializeField] public bool handlePartialTts = true;

        /// <summary>
        /// Whether or not final tts responses should be sent to attached speech handlers
        /// </summary>
        [Tooltip("Whether or not final tts responses should be sent to attached speech handlers")]
        [FormerlySerializedAs("handleTts")]
        [FormerlySerializedAs("_handleFinalTts")]
        [SerializeField] public bool handleFinalTts = false;

        /// <summary>
        /// Handles response message load and playback
        /// </summary>
        [Tooltip("Handles response message load and playback")]
        [SerializeField] protected IComposerSpeechHandler[] _speechHandlers;

        /// <summary>
        /// Whether or not the partial response actions should be handled using the action handlers
        /// </summary>
        [Header("Action Settings")]
        [Tooltip("Whether or not response actions should be handled using the action handlers")]
        [FormerlySerializedAs("_handleActions")]
        [SerializeField] public bool handleActions = true;

        /// <summary>
        /// Handles response message action calls
        /// </summary>
        [Tooltip("Handles response message action calls")]
        [SerializeField] protected IComposerActionHandler _actionHandler;

        public VoiceService VoiceService
        {
            get => _voiceService;
#if UNITY_EDITOR
            set => _voiceService = value;
#endif
        }

        /// <summary>
        /// Whether composer is currently active for the current voice request
        /// </summary>
        public bool IsComposerActive => _requests.Count > 0;

        /// <summary>
        /// All requests currently being performed
        /// </summary>
        private List<CurrentComposerRequest> _requests = new List<CurrentComposerRequest>();
        private object _requestLock = new object(); // Cannot add until safely searched for request holds

        /// <summary>
        /// Delay from action completion and response to listen or graph continuation
        /// activation
        /// </summary>
        [Header("Composer Settings")] public float continueDelay = 0f;

        /// <summary>
        /// The context_map flag name used when to identify an event vs a text/voice input.
        /// </summary>
        [Tooltip(
            "A configurable flag for use in the Composer graph to differentiate activations to the server without" +
            " text/voice input, such as a context map update. In such cases, this will be set to true. \n" +
            "For voice and text activations, this will be set to false.")]
        [SerializeField]
        public string contextMapEventKey = "state_event";

        /// <summary>
        /// Whether this service should automatically handle input
        /// activation
        /// </summary>
        public bool expectInputAutoActivation = true;

        /// <summary>
        /// Whether this service should automatically end the session
        /// on graph completion or not
        /// </summary>
        public bool endSessionOnCompletion = false;

        /// <summary>
        /// Whether this service should automatically clear the
        /// context map on graph completion or not
        /// </summary>
        public bool clearContextMapOnCompletion = false;

        /// <summary>
        /// Whether non errors should be added to VLog
        /// </summary>
        [SerializeField] public bool debug = false;

        /// <summary>
        /// Previously used for editor version tagging
        /// </summary>
        [Obsolete("Use WitConfiguration.editorVersionTag instead.")]
        [SerializeField] [HideInInspector]
        public string editorVersionTag;

        /// <summary>
        /// Previously used for build version tagging
        /// </summary>
        [Obsolete("Use WitConfiguration.buildVersionTag instead.")]
        [SerializeField] [HideInInspector]
        public string buildVersionTag;

        /// <summary>
        /// All event callbacks for Composer specific responses
        /// </summary>
        [Tooltip("Events that will fire before, during and after an activation")] [SerializeField]
        private ComposerEvents _events = new ComposerEvents();

        public ComposerEvents Events => _events;

        /// <summary>
        /// All event callbacks for Context Map specific responses
        /// </summary>
        [Tooltip("Events that will fire during Context Map change")]
        [SerializeField]
        private ContextEvents _contextEvents = new ContextEvents();

        public ContextEvents ContextMapEvents => _contextEvents;

        /// <summary>
        /// Handles activation overide & response callback
        /// </summary>
        protected abstract IComposerRequestHandler GetRequestHandler();

        /// <summary>
        /// Delegate that can be used to find what session id should be used for the specified user id
        /// </summary>
        public IComposerSessionProvider SessionProvider
        {
            get => _sessionProvider as IComposerSessionProvider;
            set
            {
                if (value is UnityEngine.Object valObj)
                {
                    _sessionProvider = valObj;
                    return;
                }
                if (value != null)
                {
                    Logger.Error("Set invalid IComposerSessionProvider type ({0})\nReason: Must inherit from {1}",
                        value?.GetType().Name ?? "Null",
                        nameof(UnityEngine.Object));
                    throw new ArgumentException("Set invalid IComposerSessionProvider type");
                }
                _sessionProvider = null;
            }
        }
        [ObjectType(typeof(IComposerSessionProvider))] [SerializeField]
        private UnityEngine.Object _sessionProvider;

        // Callback trackers
        private bool _ttsHandled;
        private HashSet<string> _actionsHandled = new HashSet<string>();

        /// <summary>
        /// A store for the current state of enablement that can be called off the main thread
        /// </summary>
        private bool _enabled;

        // Made obsolete once support for multiple simultaneous active sessions began.
        [Obsolete("Use 'LastSessionId' or 'ActiveSessions' instead.")]
        public string SessionID => LastSessionId;
        [Obsolete("Use 'ActiveSessions' instead.")]
        public DateTime SessionStart
        {
            get
            {
                if (ActiveSessions.TryGetValue(LastSessionId, out var result))
                {
                    return result.SessionStart;
                }
                return DateTime.MinValue;
            }
        }
        [Obsolete("Use 'ActiveSessions' instead.")]
        public TimeSpan SessionElapsed => (SessionStart - DateTime.UtcNow);
        #endregion

        #region LIFECYCLE

        // Initial setup
        protected virtual void Awake()
        {
            // If voice service is not found, grab from this or child game object
            if (_voiceService == null)
            {
                _voiceService = gameObject.GetComponentInChildren<VoiceService>();

                // Warn without voice service
                if (_voiceService == null)
                {
                    Log("No Voice Service found", true);
                }
            }

            // If speech handler is not found, grab from this or child game object
            if (_speechHandlers == null)
            {
                _speechHandlers = gameObject.GetComponentsInChildren<IComposerSpeechHandler>();
            }

            // If action handler is not found, grab from this or child game object
            if (_actionHandler == null)
            {
                _actionHandler = gameObject.GetComponentInChildren<IComposerActionHandler>();
            }
        }

        // Add delegates
        protected virtual void OnEnable()
        {
            _enabled = true;
            if (_voiceService != null)
            {
                _voiceService.VoiceEvents.OnRequestFinalize += RouteVoiceServiceActivation;
            }

            CurrentContextMap.OnContextMapValueChanged.AddListener(RaiseOnContextMapValueChanged);
            CurrentContextMap.OnContextMapValueRemoved.AddListener(RaiseOnContextMapValueRemoved);
        }

        // Remove delegates
        protected virtual void OnDisable()
        {
            _enabled = false;
            if (_voiceService != null)
            {
                _voiceService.VoiceEvents.OnRequestFinalize -= RouteVoiceServiceActivation;
            }

            CurrentContextMap.OnContextMapValueChanged.RemoveListener(RaiseOnContextMapValueChanged);
            CurrentContextMap.OnContextMapValueRemoved.RemoveListener(RaiseOnContextMapValueRemoved);
        }

        // Handle breakdown
        protected virtual void OnDestroy()
        {

        }

        private void LogState(string state, VoiceServiceRequest request)
        {
            if(debug) Logger.Verbose("{0} [{1}]", state, request?.Options?.RequestId ?? "Unknown ID");
        }

        // Log while editing
        protected void Log(string comment, bool error = false)
        {
            if (!error && !debug)
            {
                return;
            }
            const string message = "{0}\nScript: {1}\nGameObject: {2}\nActive Sessions: {3}";
            if (error)
            {
                Logger.Error(message,
                    comment,
                    GetType().Name,
                    gameObject == null ? "Null" : gameObject.name,
                    ActiveSessions.Count);
            }
            else if (debug)
            {
                Logger.Verbose(message,
                    comment,
                    GetType().Name,
                    gameObject == null ? "Null" : gameObject.name,
                    ActiveSessions.Count);
            }
        }

        #endregion

        #region SESSION
        /// <summary>
        /// Whether or not the specified session id is active
        /// </summary>
        public bool IsSessionActive(string sessionId)
            => !string.IsNullOrEmpty(sessionId) && ActiveSessions.ContainsKey(sessionId);

        /// <summary>
        /// Begin a new composer session.  If the specified session id
        /// is empty, a new one will be generated.
        /// </summary>
        /// <returns>The new session id</returns>
        public IComposerSession StartSession(string sessionId = null)
        {
            // Generate new session id if needed
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = GetDefaultSessionID();
            }
            // Ignore if session has already started
            if (ActiveSessions.TryGetValue(sessionId, out var session))
            {
                return session;
            }
            // End last session if desired
            if (EndLastSessionOnStart && !string.IsNullOrEmpty(LastSessionId))
            {
                EndSession(LastSessionId);
            }

            // Apply session id
            Logger.Verbose("Start Composer Session\nSession Id: {0}", sessionId);
            session = GenerateSession(sessionId);
            ActiveSessions[sessionId] = session;
            LastSessionId = sessionId;

            // Wrap session events and start session
            session.StartSession();
            Events?.OnComposerSessionBegin?.Invoke(GetSessionData(session));

            // Return session
            return session;
        }

        /// <summary>
        /// End a specified session
        /// </summary>
        public bool EndSession(string sessionId)
        {
            // Ignore if empty
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }
            // Ignore if was not active
            if (!ActiveSessions.TryRemove(sessionId, out var session))
            {
                return false;
            }

            // Remove session
            Logger.Verbose("End Composer Session\nSession Id: {0}", sessionId);

            // Remove last session id
            if (string.Equals(LastSessionId, sessionId))
            {
                LastSessionId = null;
            }

            // End session and unwrap session events
            var sessionData = GetSessionData(session);
            session.EndSession();
            Events?.OnComposerSessionEnd?.Invoke(sessionData);

            // Return success
            return true;
        }

        /// <summary>
        /// Generate a new session object using session provider if possible
        /// </summary>
        protected virtual IComposerSession GenerateSession(string sessionId)
            => new BaseComposerSession(sessionId, CurrentContextMap);

        /// <summary>
        /// Obtains session id if applicable
        /// </summary>
        protected virtual string GetSessionId(VoiceServiceRequest request)
        {
            // Attempt to get session id from response data
            if (request.ResponseData != null)
            {
                var responseSessionId = request.ResponseData[WitComposerConstants.ENDPOINT_COMPOSER_PARAM_SESSION].Value;
                if (!string.IsNullOrEmpty(responseSessionId))
                {
                    return responseSessionId;
                }
            }

            // Get session id via session provider
            var customSessionId = SessionProvider?.GetComposerSessionId(this, request);
            if (!string.IsNullOrEmpty(customSessionId))
            {
                return customSessionId;
            }

            // Use previously generated session id
            if (!string.IsNullOrEmpty(LastSessionId))
            {
                return LastSessionId;
            }

            // Use default session id
            return GetDefaultSessionID();
        }

        /// <summary>
        /// Get a default session id using a randomly generated + current timestamp
        /// </summary>
        /// <returns>session id</returns>
        public virtual string GetDefaultSessionID()
            => WitConstants.GetUniqueId();

        /// <summary>
        /// Get session data using current session id and context map
        /// </summary>
        protected virtual ComposerSessionData GetSessionData(IComposerSession session)
            => new ComposerSessionData()
            {
                session = session,
                composer = this,
                responseData = null,
            };

        /// <summary>
        /// Updates the current context map with the data from the given map.
        /// </summary>
        protected virtual void UpdateContextMap(ComposerSessionData sessionData)
        {
            if (CurrentContextMap.UpdateData(sessionData.responseData.witResponse))
            {
                RaiseContextMapChanged(sessionData);
            }
        }

        /// <summary>
        /// Performs a context map changed callback
        /// </summary>
        protected virtual void RaiseContextMapChanged(ComposerSessionData sessionData)
        {
            if (Events.OnComposerContextMapChange == null)
            {
                return;
            }
            ThreadUtility.CallOnMainThread(Logger,
                () => Events.OnComposerContextMapChange.Invoke(sessionData));
        }

        /// <summary>
        /// Performs a context map value changed callback
        /// </summary>
        protected virtual void RaiseOnContextMapValueChanged(string key, string oldValue, string newValue) {
          if (ContextMapEvents.OnContextMapValueChanged == null) {
            return;
          }
          ThreadUtility.CallOnMainThread(Logger,
              () => ContextMapEvents.OnContextMapValueChanged.Invoke(key, oldValue, newValue));
        }

        /// <summary>
        /// Performs a context map value removed callback
        /// </summary>
        protected virtual void RaiseOnContextMapValueRemoved(string key) {
          if (ContextMapEvents.OnContextMapValueRemoved == null) {
            return;
          }
          ThreadUtility.CallOnMainThread(Logger,
              () => ContextMapEvents.OnContextMapValueRemoved.Invoke(key));
        }
        #endregion

        #region HELPERS
        /// <summary>
        /// Queue of concurrent tasks to be awaited during an event
        /// </summary>
        private ConcurrentQueue<Task> _eventTtsTasks = new ConcurrentQueue<Task>();

        // Activate message
        public void Activate(string message) => _voiceService?.Activate(message);

        // Activate speech via mic volume threshold
        public void Activate() => _voiceService?.Activate();

        // Activate speech via mic without waiting for volume threshold
        public void ActivateImmediately() => _voiceService?.ActivateImmediately();

        // Deactivate speech immediately
        public void Deactivate() => _voiceService?.Deactivate();

        // Deactivate speech and ignore cancel response from server
        public void DeactivateAndAbortRequest() => _voiceService?.DeactivateAndAbortRequest();

        // Only sends a context map
        public void SendContextMapEvent()
        {
            SendEvent(string.Empty);
        }

        /// <summary>
        /// Send context map event and await until the request and subsequent responses complete
        /// </summary>
        public Task SendContextMapEvent(WitRequestOptions requestOptions)
            => SendEvent(string.Empty, requestOptions);

        /// <summary>
        /// Send an event with specified json
        /// </summary>
        public void SendEvent(string eventJson)
        {
            if (_voiceService == null)
            {
                return;
            }
            _voiceService.Activate(eventJson);
        }

        /// <summary>
        /// Send event with json and request options
        /// </summary>
        public async Task SendEvent(string eventJson, WitRequestOptions requestOptions)
        {
            // Throw an error
            if (_voiceService == null)
            {
                throw new Exception("Cannot SendEvent without VoiceService");
            }
            // Obtain options
            requestOptions ??= new WitRequestOptions();

            // Obtain voice service
            var voiceServiceRequest = await _voiceService.Activate(eventJson, requestOptions);

            // Await request completion
            await voiceServiceRequest.Completion.Task;

            // Get task list & clear for next event
            var ttsTasks = _eventTtsTasks.ToArray();
            _eventTtsTasks.Clear();

            // Throw errors
            if (voiceServiceRequest.State == VoiceRequestState.Failed)
            {
                throw new Exception(voiceServiceRequest.Results.Message);
            }
            // Throw abort
            if (voiceServiceRequest.State == VoiceRequestState.Canceled)
            {
                throw new Exception(WitConstants.CANCEL_ERROR);
            }

            // Await resultant TTS
            for (int i = 0; i < ttsTasks.Length; i++)
            {
                // Get next task
                var ttsTask = ttsTasks[i];
                if (ttsTask == null) continue;

                // Await tts load and playback
                await ttsTask;

                // Throw tts task exception
                if (ttsTask.Exception != null && ttsTask.Exception.InnerException != null)
                {
                    throw ttsTask.Exception.InnerException;
                }
            }
        }

        /// <summary>
        /// Add a task to the queue for waiting
        /// </summary>
        public void AddEventTtsTask(Task ttsTask)
        {
            if (_eventTtsTasks != null)
            {
                _eventTtsTasks.Enqueue(ttsTask);
            }
        }

        private bool IsRequestTracked(VoiceServiceRequest request)
        {
            return _requests.FirstOrDefault((compRequest)
                => compRequest?.Request != null && compRequest.Request.Equals(request)) != null;
        }

    #endregion

        #region REQUEST
        /// <summary>
        /// Routes voice service request into composer if desired
        /// </summary>
        protected virtual void RouteVoiceServiceActivation(VoiceServiceRequest request)
        {
            // If disabled, do not perform composer request
            if (!RouteVoiceServiceToComposer)
            {
                return;
            }

            // Ensure request is not being tracked
            if (IsRequestTracked(request))
            {
                return;
            }

            // If not active, cancel request
            if (!_enabled)
            {
                request.Cancel("Composer disabled");
                return;
            }

            // Determine session id
            var sessionId = GetSessionId(request);

            // Get session if possible, otherwise start new session
            if (!ActiveSessions.TryGetValue(sessionId, out var session))
            {
                session = StartSession(sessionId);
            }

            // Generate composer request & add to request list
            var sessionData = GetSessionData(session);

            // Generate request for tracking
            var composerRequest = new CurrentComposerRequest(this, request, sessionData);

            // Lock to ensure requests dont update while querying
            lock (_requestLock)
            {
                // Get all completion tasks for the same session id
                request.HoldTask = Task.WhenAll(_requests.Select((check) =>
                {
                    if (string.Equals(sessionData.sessionID, check?.SessionId))
                    {
                        return check?.Request?.Completion?.Task;
                    }
                    return null;
                }));
                // Add request to list
                _requests.Add(composerRequest);
            }

            // Activation event
            RaiseRequestCreated(sessionData, request);

            // Init complete
            SetupComposerRequest(sessionData, request);
        }

        // Handle Setup of composer request and perform init callback
        protected virtual void SetupComposerRequest(ComposerSessionData sessionData, VoiceServiceRequest request)
        {
            IComposerRequestHandler requestHandler = GetRequestHandler();
            if (requestHandler != null)
            {
                requestHandler.OnComposerRequestSetup(sessionData, request);
            }
            RaiseRequestSetup(sessionData, request);
        }

        /// <summary>
        /// Callback following request creation
        /// </summary>
        protected virtual void RaiseRequestCreated(ComposerSessionData sessionData, VoiceServiceRequest request)
        {
            LogState("Request Setup", request);
            Events?.OnComposerRequestCreated?.Invoke(sessionData, request);
            ThreadUtility.CallOnMainThread(() => Events?.OnComposerActivation?.Invoke(sessionData)).WrapErrors();
        }

        /// <summary>
        /// Callback following request setup
        /// </summary>
        protected virtual void RaiseRequestSetup(ComposerSessionData sessionData, VoiceServiceRequest request)
        {
            LogState("Request Setup", request);
            Events.OnComposerRequestInit?.Invoke(sessionData);
            Events.OnComposerRequestSetup?.Invoke(sessionData, request);
        }

        // Handle sending of data
        protected virtual void OnVoiceRequestSend(ComposerSessionData sessionData, VoiceServiceRequest request)
        {
            LogState("Request Send", request);
            Events.OnComposerRequestBegin?.Invoke(sessionData);
        }

        // Handle Partial Resposne
        protected virtual void OnVoicePartialResponse(ComposerSessionData sessionData)
        {
            // Update context map if applicable
            UpdateContextMap(sessionData);

            // Read phrase if possible
            if (!string.IsNullOrEmpty(sessionData.responseData.responsePhrase))
            {
                _ttsHandled |= OnComposerSpeakPhrase(sessionData);
            }

            var actionId = sessionData.responseData?.actionID;

            // Perform action if possible
            if (!_actionsHandled.Contains(actionId) && !string.IsNullOrEmpty(actionId))
            {
                if (OnComposerPerformAction(sessionData)) {
                    _actionsHandled.Add(actionId);
                }
            }

            if (sessionData.responseData?.witResponse[WitComposerConstants.RESPONSE_TYPE_FULL_COMPOSER].AsBool ?? false) {
              // If we've hit the end of a response node clear the fact that any actions have been executed. We may have
              // duplicate follow up actions, but they will come from a new response node.
              _actionsHandled.Clear();
            }
        }

        // Handle completion
        private void OnVoiceRequestComplete(CurrentComposerRequest composerRequest)
        {
            // Warn if cannot remove
            _ = ThreadUtility.BackgroundAsync(Logger, async () =>
            {
                lock (_requestLock)
                {
                    if (!_requests.Remove(composerRequest))
                    {
                        Logger.Warning("Completed composer request not found\nId: {0}", composerRequest?.Request?.Options?.RequestId ?? "Null");
                    }
                }

                // Call on main thread
                await ThreadUtility.CallOnMainThread(() =>
                {
                    // Cancelled
                    if (composerRequest.Request.State == VoiceRequestState.Canceled)
                    {
                        OnComposerCanceled(composerRequest.SessionData, composerRequest.Request.Results.Message);
                    }
                    // Failed
                    else if (composerRequest.Request.State == VoiceRequestState.Failed)
                    {
                        OnComposerError(composerRequest.SessionData, composerRequest.Request.Results.Message);
                    }
                    // Request Successful
                    else if (composerRequest.Request.State == VoiceRequestState.Successful)
                    {
                        OnComposerResponse(composerRequest.SessionData, composerRequest.Request.ResponseData);
                    }

                    // Log completion
                    LogState("Request Complete", composerRequest.Request);
                });
            });
        }
        #endregion

        #region RESPONSE
        // Composer request setup
        protected virtual void OnComposerCanceled(ComposerSessionData sessionData, string reason)
        {
            // Error response
            sessionData.responseData = new ComposerResponseData(reason);

            // Error callback
            if(debug) Logger.Verbose($"Request Canceled\nReason: {0}", sessionData.responseData.error);
            Events.OnComposerCanceled?.Invoke(sessionData);
        }

        // Handle composer error
        protected virtual void OnComposerError(ComposerSessionData sessionData, string error)
        {
            // Error response
            sessionData.responseData = new ComposerResponseData(error);

            // Error callback
            if(debug) Logger.Error("Request Error\nError: {0}\n{1}", sessionData.responseData.error, sessionData.responseData.witResponse);
            Events.OnComposerError?.Invoke(sessionData);
        }

        // Final composer response returned via json
        protected virtual void OnComposerResponse(ComposerSessionData sessionData, WitResponseNode response)
        {
            var actionId = sessionData.responseData?.actionID;
            // Parse response data if not set by partial response
            if (response != sessionData.responseData?.witResponse)
            {
                sessionData.responseData = response.GetComposerResponse();
                OnVoicePartialResponse(sessionData);
            }
            // Perform action if not yet performed
            else if (!string.IsNullOrEmpty(actionId) && !_actionsHandled.Contains(actionId))
            {
                if (OnComposerPerformAction(sessionData))
                {
                    _actionsHandled.Add(actionId);
                }
            }

            // Response event
            Log("Request Success");
            Events.OnComposerResponse?.Invoke(sessionData);

            // Continue if tts or action was handled
            var needsContinue = _ttsHandled || _actionsHandled.Count > 0;
            _ttsHandled = false;
            _actionsHandled.Clear();

            // Expect input once complete
            if (sessionData.responseData != null && sessionData.responseData.expectsInput)
            {
                needsContinue = true;
            }

            // Wait to continue the composer
            if (needsContinue)
            {
                CoroutineUtility.StartCoroutine(WaitToContinue(sessionData));
            }
        }

        // Speak phrase callback & handle with speech handler
        protected virtual bool OnComposerSpeakPhrase(ComposerSessionData sessionData)
        {
            // Ignore partial or final if desired
            bool isFinal = sessionData.responseData.responseIsFinal;
            if (!isFinal && !handlePartialTts)
            {
                return false;
            }
            if (isFinal && !handleFinalTts)
            {
                return false;
            }

            // Perform phrase callback
            if(debug) Logger.Verbose($"Perform Speak\nPhrase: {0}\nFinal Response: {1}", sessionData.responseData.responsePhrase, isFinal);
            Events.OnComposerSpeakPhrase?.Invoke(sessionData);

            // Handle phrase if possible
            for (int i = 0; null != _speechHandlers && i < _speechHandlers.Length; i++)
            {
                var speechHandler = _speechHandlers[i];
                speechHandler.SpeakPhrase(sessionData);
            }
            return true;
        }

        // Perform action
        protected virtual bool OnComposerPerformAction(ComposerSessionData sessionData)
        {
            // Ignore if not
            if (!handleActions)
            {
                return false;
            }

            // Perform action callback
            if(debug) Logger.Verbose("Perform Action\nAction: {0}", sessionData?.responseData?.actionID);
            Events.OnComposerPerformAction?.Invoke(sessionData);

            // Handle action if possible
            if (_actionHandler != null)
            {
                _actionHandler.PerformAction(sessionData);
            }

            // Handled
            return true;
        }

        // Perform expect input
        protected virtual void OnComposerExpectsInput(ComposerSessionData sessionData)
        {
            // Perform action callback
            Log($"Expects Input");
            Events.OnComposerExpectsInput?.Invoke(sessionData);

            // Activate voice service
            if (expectInputAutoActivation && _voiceService != null)
            {
                _voiceService.Activate();
            }
        }

        // Composer graph completed
        protected virtual void OnComposerComplete(ComposerSessionData sessionData)
        {
            Log($"Graph Complete");
            Events.OnComposerComplete?.Invoke(sessionData);

            // End session on completion
            if (endSessionOnCompletion)
            {
                EndSession(sessionData.sessionID);
            }
            // Clear context map on completion
            if (clearContextMapOnCompletion)
            {
                CurrentContextMap.ClearAllNonReservedData();
            }
        }
        #endregion

        #region AUTO ACTIVATION
        /// <summary>
        /// Perform coroutine to wait for completion & then auto activate once we are ready for the next turn in the
        /// conversation flow
        /// </summary>
        /// <param name="sessionData">The current session that is in progress</param>
        private IEnumerator WaitToContinue(ComposerSessionData sessionData)
        {
            // Wait for everything to continue
            Log($"Wait to Continue - Begin");
            yield return null; // Needs an initial wait to ensure data was returned
            yield return new WaitUntil(() => IsContinueAllowed(sessionData));
            yield return new WaitForSeconds(continueDelay);
            Log($"Wait to Continue - Complete");

            // Call expects input
            if (sessionData.responseData.expectsInput)
            {
                OnComposerExpectsInput(sessionData);
            }
            // Nowhere to go, complete session
            else
            {
                OnComposerComplete(sessionData);
            }
        }

        // Whether continue should be allowed
        protected virtual bool IsContinueAllowed(ComposerSessionData sessionData)
        {
            // Wait for service to stop being active
            if (_voiceService.IsRequestActive)
            {
                return false;
            }

            for (int i = 0; null != _speechHandlers && i < _speechHandlers.Length; i++)
            {
                var speechHandler = _speechHandlers[i];
                // Wait for speech handler completion if applicable
                if (speechHandler.IsSpeaking(sessionData))
                {
                    return false;
                }
            }

            // Wait for action handler completion if applicable
            if (_actionHandler != null && _actionHandler.IsPerformingAction(sessionData))
            {
                return false;
            }
            // Input allowed
            return true;
        }
        #endregion

        /// <summary>
        /// Handles subscribing/unsubscribing to events for an active composer session request.
        /// </summary>
        private class CurrentComposerRequest
        {

            private ComposerService _service;
            public VoiceServiceRequest Request { get; private set; }
            public readonly ComposerSessionData SessionData;
            public string SessionId => SessionData?.sessionID;
            public bool IsActive => Request == null ? false : Request.IsActive;

            public CurrentComposerRequest(ComposerService service, VoiceServiceRequest request, ComposerSessionData sessionData)
            {
                _service = service;
                SessionData = sessionData;
                SessionData.responseData = new ComposerResponseData();
                Request = request;
                Request.Events.OnSend.AddListener(OnSend);
                Request.Events.OnPartialResponse.AddListener(OnPartial);
                Request.Events.OnValidateResponse.AddListener(OnValidate);
                Request.Events.OnComplete.AddListener(OnComplete);
                if (Request.ResponseData != null)
                {
                    OnPartial(Request.ResponseData);
                }
            }

            private void OnSend(VoiceServiceRequest r)
            {
                UpdateResponseData(r.ResponseData);
                _service.OnVoiceRequestSend(SessionData, r);
            }

            private void OnPartial(WitResponseNode r)
            {
                UpdateResponseData(r);
                _service.OnVoicePartialResponse(SessionData);
            }

            private void OnValidate(WitResponseNode r, StringBuilder validationErrors)
            {
                var responseObject = r?.AsObject;
                if (!(responseObject?.HasChild(WitComposerConstants.ENDPOINT_COMPOSER_PARAM_CONTEXT_MAP) ?? false))
                {
                    if (validationErrors.Length > 0)
                    {
                        validationErrors.Append(", ");
                    }
                    validationErrors.Append("missing context map");
                }
            }

            private void OnComplete(VoiceServiceRequest r)
            {
                Request.Events.OnSend.RemoveListener(OnSend);
                Request.Events.OnPartialResponse.RemoveListener(OnPartial);
                Request.Events.OnValidateResponse.RemoveListener(OnValidate);
                Request.Events.OnComplete.RemoveListener(OnComplete);
                try
                {
                    UpdateResponseData(r.ResponseData);
                    _service.OnVoiceRequestComplete(this);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Update Failure\n{e}");
                }
            }

            /// <summary>
            /// Update response node with decoded composer response
            /// </summary>
            private void UpdateResponseData(WitResponseNode r)
            {
                SessionData.responseData = r.GetComposerResponse();
            }
        }
    }
}
