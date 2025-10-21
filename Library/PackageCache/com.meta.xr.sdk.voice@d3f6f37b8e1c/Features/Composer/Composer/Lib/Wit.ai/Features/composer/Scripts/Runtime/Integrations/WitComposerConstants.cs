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

namespace Meta.WitAi.Composer.Integrations
{
    public enum WitComposerMessageType
    {
        Message
    }

    public static class WitComposerConstants
    {
        /// <summary>
        /// Path is reserved for shallow path based flows in Composer
        /// </summary>
        public static string CONTEXT_MAP_RESERVED_PATH = "path";

        // Composer Endpoints & parameters
        public const string ENDPOINT_COMPOSER_SPEECH = "converse";
        public const string ENDPOINT_COMPOSER_PARAM_SESSION = "session_id";
        public const string ENDPOINT_COMPOSER_PARAM_CONTEXT_MAP = "context_map";
        public const string ENDPOINT_COMPOSER_PARAM_DEBUG = "debug";
        public const string ENDPOINT_COMPOSER_MESSAGE = "event";
        public const string ENDPOINT_COMPOSER_MESSAGE_PARAM_MESSAGE = "message";
        public const string ENDPOINT_COMPOSER_MESSAGE_PARAM_TYPE = "type";
        public const string ENDPOINT_COMPOSER_MESSAGE_TAG = "tag";

        // Request options used for platform integration
        public const string PI_COMPOSER_ENABLE = "useComposer";
        public const string PI_COMPOSER_ENABLE_ON = "True";

        /// <summary>
        /// Request option to only preload at server side. No response data will be returned.
        /// </summary>
        public static string PRELOAD = "preload";

        // Response parsing
        /// <summary>
        /// Returned if the response is a partial response from composer before the full execution of a response node.
        /// </summary>
        public const string RESPONSE_TYPE_PARTIAL_COMPOSER = "PARTIAL_COMPOSER";
        /// <summary>
        /// Type is full composer if the response received is the last response from a response node.
        ///
        /// NOTE: This is not the final response and the isFinal flag may or may not be true depending if there is more
        /// in the graph to execute.
        /// </summary>
        public const string RESPONSE_TYPE_FULL_COMPOSER = "FULL_COMPOSER";
        public const string RESPONSE_NODE_EXPECTS_INPUT = "expects_input";
        public const string RESPONSE_NODE_ACTION = WitConstants.KEY_RESPONSE_ACTION;
        public const string RESPONSE_NODE_TEXT = "text";
        public const string RESPONSE_NODE_SPEECH = "speech";
        public const string RESPONSE_NODE_Q = "q";
    }
}
