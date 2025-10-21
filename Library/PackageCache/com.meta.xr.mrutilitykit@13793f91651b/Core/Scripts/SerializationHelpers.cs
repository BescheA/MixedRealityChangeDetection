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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using Meta.XR.Util;

namespace Meta.XR.MRUtilityKit
{
    /// <summary>
    /// This class offers the enums used to configure the scene serialization and contains internal data structures, classes and utility methods
    /// used for serialization and deserialization.
    /// <see cref="MRUK.SaveSceneToJsonString(SerializationHelpers.CoordinateSystem, bool, System.Collections.Generic.List{MRUKRoom})"/>
    /// for more information on how to serialize and export the scene data and <see cref="MRUK.LoadSceneFromJsonString(string)"/>
    /// for more information on how to import the JSON scene data.
    /// </summary>
    [Feature(Feature.Scene)]
    public static class SerializationHelpers
    {
        /// <summary>
        /// Defines the coordinate systems that can be used for
        /// to set the flavor of the scene data that is being imported or exported.
        /// Possible options are:
        /// <list type="bullet">
        /// <item>
        /// <term><see cref="CoordinateSystem.Unity"/></term>
        /// <description>Unity coordinate system.</description>
        /// </item>
        /// <item>
        /// <term><see cref="CoordinateSystem.Unreal"/></term>
        /// <description>Unreal Engine coordinate system.</description>
        /// </item>
        /// </list>
        /// </summary>
        [Serializable, JsonConverter(typeof(StringEnumConverter))]
        [Obsolete("Coordinate system is now obsolete, JSON files are now always serialized in OpenXR coordinate system")]
        public enum CoordinateSystem
        {
            Unity, // Unity coordinate system
            Unreal, // Unreal Engine coordinate system
        }


    }
}
