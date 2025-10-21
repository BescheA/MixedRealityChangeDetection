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
using Meta.XR.Util;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Meta.XR.MRUtilityKit
{
    /// <summary>
    /// A "trackable" is a type of anchor that can be detected and tracked by the runtime.
    /// </summary>
    /// <remarks>
    /// Trackables are instantiated and managed by <see cref="MRUK"/>; you should not add this component
    /// to your own `GameObject`s.
    ///
    /// When <see cref="MRUK"/> detects a new trackable, it will invoke its <see cref="MRUK.MRUKSettings.TrackableAdded"/> event and provide an instance of
    /// <see cref="MRUKTrackable"/> to its subscribers.
    /// </remarks>
    [Feature(Feature.TrackedKeyboard)]
    public sealed class MRUKTrackable : MRUKAnchor
    {
        /// <summary>
        /// This specific type of trackable this <see cref="MRUKTrackable"/> represents.
        /// </summary>
        public OVRAnchor.TrackableType TrackableType { get; private set; }

        /// <summary>
        /// Whether this trackable is current considered tracked.
        /// </summary>
        /// <remarks>
        /// A trackable may become temporarily untracked if, for example, it cannot
        /// be seen by the device.
        /// </remarks>
        public bool IsTracked { get; internal set; }

        /// <summary>
        /// The marker's payload as a string.
        /// </summary>
        /// <remarks>
        /// If this trackable is a marker (e.g., a QR Code) and its payload can be interpreted as a string,
        /// use this property to get the payload as a string.
        ///
        /// If this trackable is not a marker, or its payload is not a string, this property is `null`.
        /// </remarks>
        /// <seealso cref="MarkerPayloadBytes"/>
        public string MarkerPayloadString { get; private set; }

        /// <summary>
        /// The marker's payload as raw bytes.
        /// </summary>
        /// <remarks>
        /// If this trackable is a marker (e.g., a QR Code) use this property to get the payload bytes.
        ///
        /// If this trackable is not a marker, then this property is `null`.
        /// </remarks>
        /// <seealso cref="MarkerPayloadString"/>
        public byte[] MarkerPayloadBytes { get; private set; }

        // Invoked by MRUK when a fetch (FetchTrackablesAsync) completes, as this could have updated some component data
        internal void OnFetch()
        {
            TrackableType = Anchor.GetTrackableType();

            using (new OVRObjectPool.ListScope<OVRPlugin.SpaceComponentType>(out var supportedComponents))
            {
                if (!Anchor.GetSupportedComponents(supportedComponents))
                {
                    return;
                }

                foreach (var componentType in supportedComponents)
                {
                    if (!OVRPlugin.GetSpaceComponentStatus(Anchor.Handle, componentType, out var isEnabled, out _) ||
                        !isEnabled)
                    {
                        continue;
                    }

                    switch (componentType)
                    {
                        case OVRPlugin.SpaceComponentType.MarkerPayload:
                        {
                            var component = Anchor.GetComponent<OVRMarkerPayload>();
                            using var buffer = new NativeArray<byte>(component.ByteCount, Allocator.Temp);
                            Span<byte> bytes;
                            unsafe
                            {
                                var temp = new Span<byte>((byte*)buffer.GetUnsafePtr(), buffer.Length);
                                bytes = temp[..component.GetBytes(temp)];
                            }

                            if (!bytes.SequenceEqual(MarkerPayloadBytes))
                            {
                                MarkerPayloadBytes = bytes.ToArray();
                                MarkerPayloadString = component.PayloadType == OVRMarkerPayloadType.StringQRCode
                                    ? System.Text.Encoding.UTF8.GetString(MarkerPayloadBytes)
                                    : null;
                            }

                            break;
                        }
                        case OVRPlugin.SpaceComponentType.Bounded2D:
                        {
                            static List<Vector2> GetUpdatedBoundary(OVRBounded2D component, List<Vector2> currentBoundary)
                            {
                                if (component.TryGetBoundaryPointsCount(out var count))
                                {
                                    using var newBoundary = new NativeArray<Vector2>(count, Allocator.Temp);
                                    if (component.TryGetBoundaryPoints(newBoundary))
                                    {
                                        // Only allocate a new list if we need to.
                                        currentBoundary ??= new(capacity: newBoundary.Length);
                                        currentBoundary.Clear();
                                        foreach (var point in newBoundary)
                                        {
                                            currentBoundary.Add(point);
                                        }

                                        return currentBoundary;
                                    }
                                }

                                return null;
                            }

                            var component = Anchor.GetComponent<OVRBounded2D>();
                            PlaneRect = component.BoundingBox;
                            PlaneBoundary2D = GetUpdatedBoundary(component, PlaneBoundary2D);

                            break;
                        }
                        case OVRPlugin.SpaceComponentType.Bounded3D:
                        {
                            VolumeBounds = Anchor.GetComponent<OVRBounded3D>().BoundingBox;
                            break;
                        }
                    }
                }
            }
        }

        // Invoked by MRUK when this MRUKTrackable is first instantiated
        internal void OnInstantiate(OVRAnchor anchor)
        {
            Anchor = anchor;
            IsTracked = true;
            OnFetch();
        }
    }
}
