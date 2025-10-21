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
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Simulator.Editor
{
    internal static class Styles
    {
        private const int Radius = 8;
        private static Texture2D _backgroundTexture;
        private static Texture2D BackgroundTexture => _backgroundTexture ??= MakeTex(Radius);

        private const int ButtonRadius = 3;
        private static Texture2D _backgroundButtonTexture;
        private static Texture2D BackgroundButtonTexture => _backgroundButtonTexture ??= MakeTex(ButtonRadius);

        private static readonly GUIStyle ContainerStyle = new()
        {
            margin = new RectOffset(16, 16, 16, 16),
            padding = new RectOffset(16, 16, 16, 16),
            stretchHeight = false,
            normal = { background = BackgroundTexture },
            border = new RectOffset(Radius, Radius, Radius, Radius)
        };

        private static readonly GUIStyle TextStyleBold = new(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.white },
            fontSize = 12,
            wordWrap = true
        };

        private static readonly GUIStyle TextStyleBoldCentered = new(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.white },
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 20
        };

        private static readonly GUIStyle TextStyle = new(EditorStyles.label)
        {
            normal = { textColor = Color.white },
            fontSize = 12,
            wordWrap = true,
        };

        private static readonly GUIStyle ButtonStyle = new()
        {
            fixedHeight = 24,
            fixedWidth = 64,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            normal = { background = BackgroundButtonTexture },
            border = new RectOffset(ButtonRadius, ButtonRadius, ButtonRadius, ButtonRadius)
        };

        private static Texture2D MakeTex(int radius)
        {
            int size = 256;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (Utils.IsInsideRoundedRect(x, y, size, size, radius))
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear; // Transparent
                    }
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;

        }

        public static void DrawUpdateNotice(Action onUpdateClicked)
        {
            var pervIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            // Main container style

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
            EditorGUILayout.BeginHorizontal(ContainerStyle);
            GUI.backgroundColor = prevColor;

            EditorGUILayout.BeginVertical();
            // Display current version
            EditorGUILayout.LabelField("Software Update", TextStyleBold);
            EditorGUILayout.LabelField("Meta XR Simulator includes improvements and bug fixes.", TextStyle);
            if (EditorGUILayout.LinkButton("Release Notes"))
            {
                Application.OpenURL(XRSimConstants.ReleaseNotesUrl);
            }
            EditorGUILayout.EndVertical();

            // Update button
            EditorGUILayout.BeginVertical(GUILayout.Width(ButtonStyle.fixedWidth));
            prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.0f, 0.41f, 0.85f, 1.0f);
            var buttonRect = EditorGUILayout.BeginVertical(ButtonStyle);
            GUI.backgroundColor = prevColor;
            var hover = buttonRect.Contains(Event.current.mousePosition);
            {
                EditorGUILayout.BeginHorizontal(GUIStyle.none);

                EditorGUILayout.LabelField("Update", TextStyleBoldCentered);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
            if (hover && Event.current.type == EventType.MouseUp)
            {
                onUpdateClicked.Invoke();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel = pervIndent;
        }

    }
}
