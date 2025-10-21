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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Simulator.Editor
{

    public enum Origin
    {
        Unknown = -1,
        Settings,
        Menu,
        StatusMenu,
        Console,
        Component,
        Toolbar,
        Updater
    }


    [InitializeOnLoad]
    internal static class Utils
    {
        public static ProcessUtils ProcessUtils { get; set; }
        public static LogUtils LogUtils { get; set; }
        public static SystemUtils SystemUtils { get; set; }
        public static PackageManagerUtils PackageManagerUtils { get; set; }
        public static XRSimUtils XRSimUtils { get; set; }

        public static VersionUtils VersionUtils { get; set; }

        public static AuthUtils AuthUtils { get; set; }

        static Utils()
        {
            ProcessUtils = new ProcessUtils();
            LogUtils = new LogUtils();
            SystemUtils = new SystemUtils();
            PackageManagerUtils = new PackageManagerUtils();
            XRSimUtils = new XRSimUtils();
            VersionUtils = new VersionUtils();
            AuthUtils = new AuthUtils();
        }

        public static Origin ToSimulatorOrigin(this string origin)
        {
            if (!Enum.TryParse(origin, out Origin simulatorOrigin))
            {
                return Origin.Unknown;
            }
            return simulatorOrigin;
        }

        // From http://csharptest.net/529/how-to-correctly-escape-command-line-arguments-in-c/index.html
        readonly static Regex invalidChar = new("[\x00\x0a\x0d]");//  these can not be escaped
        readonly static Regex needsQuotes = new(@"\s|""");//          contains whitespace or two quote characters
        readonly static Regex escapeQuote = new(@"(\\*)(""|$)");//    one or more '\' followed with a quote or end of string
        /// <summary>
        /// Quotes all arguments that contain whitespace, or begin with a quote and returns a single
        /// argument string for use with Process.Start().
        /// </summary>
        /// <param name="args">A list of strings for arguments, may not contain null, '\0', '\r', or '\n'</param>
        /// <returns>The combined list of escaped/quoted strings</returns>
        /// <exception cref="System.ArgumentNullException">Raised when one of the arguments is null</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Raised if an argument contains '\0', '\r', or '\n'</exception>
        public static string EscapeArguments(params string[] args)
        {
            StringBuilder arguments = new StringBuilder();

            for (int carg = 0; args != null && carg < args.Length; carg++)
            {
                if (args[carg] == null) { throw new ArgumentNullException("args[" + carg + "]"); }
                if (invalidChar.IsMatch(args[carg])) { throw new ArgumentOutOfRangeException("args[" + carg + "]"); }
                if (args[carg] == String.Empty) { arguments.Append("\"\""); }
                else if (!needsQuotes.IsMatch(args[carg]))
                {
                    arguments.Append(args[carg]);
                }
                else
                {
                    arguments.Append('"');
                    arguments.Append(escapeQuote.Replace(args[carg], m =>
                    m.Groups[1].Value + m.Groups[1].Value +
                    (m.Groups[2].Value == "\"" ? "\\\"" : "")
                    ));
                    arguments.Append('"');
                }
                if (carg + 1 < args.Length)
                    arguments.Append(' ');
            }
            return arguments.ToString();
        }

        public static int GetMajorVersion(string versionString)
        {
            var version = 0;
            var versionParts = versionString.Split('.');
            if (versionParts.Length > 0)
            {
                int.TryParse(versionParts[0], out version);
            }

            return version;
        }

        public static int CompareVersions(string version1, string version2)
        {
            // discard everything after -
            var inx1 = version1.IndexOf('-');
            var v1 = inx1 == -1 ? version1 : version1.Substring(0, inx1);
            var inx2 = version2.IndexOf('-');
            var v2 = inx2 == -1 ? version2 : version2.Substring(0, inx2);
            // Split the version strings into parts
            var v1Parts = v1.Split('.');
            var v2Parts = v2.Split('.');

            // Determine the maximum length of the version parts
            int maxLength = Math.Max(v1Parts.Length, v2Parts.Length);

            // Compare each part of the version strings
            for (int i = 0; i < maxLength; i++)
            {
                // Get the current part of each version, defaulting to 0 if not present
                int v1Part = 0;
                if (i < v1Parts.Length)
                {
                    int.TryParse(v1Parts[i], out v1Part);
                }

                int v2Part = 0;
                if (i < v2Parts.Length)
                {
                    int.TryParse(v2Parts[i], out v2Part);
                }

                if (v1Part == v2Part)
                {
                    continue;
                }

                return v1Part < v2Part ? -1 : 1;
            }

            // If all parts are equal, the versions are the same
            return 0;

        }

        public static string GetMaxVersion(string[] versions)
        {
            if (versions == null || versions.Length == 0)
            {
                return "";
            }

            return versions.OrderBy(x => x, Comparer<string>.Create(CompareVersions)).LastOrDefault();
        }

        public static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
        {

            // Calculate distances from the edges
            float dx = Mathf.Min(x, width - 1 - x);
            float dy = Mathf.Min(y, height - 1 - y);

            // If the pixel is within the corner radius, check its distance from the corner
            if (dx < radius && dy < radius)
            {
                return (radius - dx) * (radius - dx) + (radius - dy) * (radius - dy) <= radius * radius;
            }

            return true;
        }
    }

}
