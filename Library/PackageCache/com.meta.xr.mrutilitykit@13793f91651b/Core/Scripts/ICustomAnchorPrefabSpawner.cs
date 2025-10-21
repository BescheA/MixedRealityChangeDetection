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

using System.Collections.Generic;
using UnityEngine;

namespace Meta.XR.MRUtilityKit
{
    /// <summary>
    ///     Defines methods for custom selection, scaling, and alignment of prefabs.
    ///     This interface is intended to be implemented by a class that extends <see cref="AnchorPrefabSpawner" />, allowing
    ///     for custom logic to be applied when selecting, scaling, and aligning prefabs. Once the interface is implemented,
    ///     make sure to set the <see cref="AnchorPrefabSpawner.SelectionMode"/>, <see cref="AnchorPrefabSpawner.AlignMode"/>
    ///     and <see cref="AnchorPrefabSpawner.ScalingMode"/>  to Custom.
    ///     The prefab selected can also be modified before being returned.
    ///     <example>
    /// <code><![CDATA[
    /// public class CustomPrefabSpawner : AnchorPrefabSpawner {
    ///     public override GameObject CustomPrefabSelection(MRUKAnchor anchor, List<GameObject> prefabs)
    ///     {
    ///         prefabs[0].GetComponentInChildren<Collider>().enabled = true;
    ///         return prefabs[0];
    ///     }
    /// }
    /// ]]></code></example>
    /// </summary>
    public interface ICustomAnchorPrefabSpawner
    {
        /// <summary>
        ///     Selects a prefab based on custom logic.
        ///     This method is intended to be overridden in a class that extends <see cref="AnchorPrefabSpawner" /> with custom logic for selecting a prefab.
        ///     It will be called whenever the PrefabSelection of an AnchorPrefabGroup is set to Custom.
        /// </summary>
        /// <param name="anchor">The anchor info to use.</param>
        /// <param name="prefabs">The list of prefabs to select from. Can be null.</param>
        /// <returns>The selected prefab.</returns>
        GameObject CustomPrefabSelection(MRUKAnchor anchor, List<GameObject> prefabs);

        /// <summary>
        ///     Scales a prefab based on custom logic.
        ///     This method is intended to be overridden in a class that extends <see cref="AnchorPrefabSpawner" />  with custom logic for scaling a prefab.
        ///     If not overridden, it throws a NotImplementedException.
        /// </summary>
        /// <param name="localScale">The local scale of the prefab.</param>
        /// <returns>The scaled local scale of the prefab.</returns>
        Vector3 CustomPrefabScaling(Vector3 localScale);

        /// <summary>
        ///     Scales a prefab based on custom logic.
        ///     This method is intended to be overridden a class that extends <see cref="AnchorPrefabSpawner" />  with custom logic for scaling a prefab.
        ///     If not overridden, it throws a NotImplementedException.
        /// </summary>
        /// <param name="localScale">The local scale of the prefab.</param>
        /// <returns>The scaled local scale of the prefab.</returns>
        Vector2 CustomPrefabScaling(Vector2 localScale);

        /// <summary>
        ///     Aligns a prefab to an anchor's volume, based on custom logic.
        ///     This method is intended to be overridden a class that extends <see cref="AnchorPrefabSpawner" />  with custom logic for aligning a prefab.
        ///     If not overridden, it throws a NotImplementedException.
        /// </summary>
        /// <param name="anchorVolumeBounds">The volume bounds of the anchor.</param>
        /// <param name="prefabBounds">The bounds of the prefab.</param>
        /// <returns>The local position of the prefab defined as the difference between the anchor's pivot and the prefab's pivot.</returns>
        Vector3 CustomPrefabAlignment(Bounds anchorVolumeBounds, Bounds? prefabBounds);

        /// <summary>
        ///     Aligns a prefab to an anchor's plane rect, based on custom logic.
        ///     This method is intended to be overridden a class that extends <see cref="AnchorPrefabSpawner" />  with custom logic for aligning a prefab.
        ///     If not overridden, it throws a NotImplementedException.
        /// </summary>
        /// <param name="anchorPlaneRect">The volume bounds of the anchor.</param>
        /// <param name="prefabBounds">The bounds of the prefab.</param>
        /// <returns>The local position of the prefab defined as the difference between the anchor's pivot and the prefab's pivot.</returns>
        Vector3 CustomPrefabAlignment(Rect anchorPlaneRect, Bounds? prefabBounds);
    }
}
