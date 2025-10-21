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
using Meta.WitAi.Composer.Interfaces;

namespace Meta.WitAi.Composer.Data
{
    /// <summary>
    /// The base composer session class for tracking requests and session
    /// specific data.
    /// </summary>
    public class BaseComposerSession : IComposerSession
    {
        /// <inheritdoc />
        public string SessionId { get; }

        /// <inheritdoc />
        public ComposerContextMap ContextMap { get; }

        /// <inheritdoc />
        public bool HasStarted { get; private set; }

        /// <inheritdoc />
        public DateTime SessionStart { get; private set; }

        /// <summary>
        /// Constructor that sets up all composer specific data
        /// </summary>
        public BaseComposerSession(string sessionId, ComposerContextMap contextMap)
        {
            HasStarted = false;
            SessionId = sessionId;
            ContextMap = contextMap;
            SessionStart = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public void StartSession()
        {
            if (HasStarted)
            {
                return;
            }
            HasStarted = true;
            SessionStart = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public void EndSession()
        {
            if (!HasStarted)
            {
                return;
            }
            HasStarted = false;
        }
    }
}
