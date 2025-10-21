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
using Meta.WitAi.Composer.Data;

namespace Meta.WitAi.Composer.Interfaces
{
    /// <summary>
    /// An interface for session specific composer data
    /// </summary>
    public interface IComposerSession
    {
        /// <summary>
        /// The unique session id used for session management
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// The current session's context map
        /// </summary>
        ComposerContextMap ContextMap { get; }

        /// <summary>
        /// Whether the session has started or not
        /// </summary>
        bool HasStarted { get; }

        /// <summary>
        /// The utc datetime of the first request using this session id
        /// </summary>
        DateTime SessionStart { get; }

        /// <summary>
        /// Method called to be begin session
        /// </summary>
        void StartSession();

        /// <summary>
        /// Method called to be end session
        /// </summary>
        void EndSession();
    }
}
