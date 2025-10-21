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
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Meta.XR.MRUtilityKit
{
    internal static class MRUKNative
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [DllImport("libdl.dylib")]
        public static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.dylib")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.dylib")]
        public static extern int dlclose(IntPtr handle);
#elif UNITY_ANDROID
        [DllImport("libdl.so")]
        public static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.so")]
        public static extern int dlclose(IntPtr handle);
#else
#warning "Unsupported platform, mr utility kit will still compile but you will get errors at runtime if you try to use it"
#endif
        private static IntPtr _nativeLibraryPtr;

        // Cross-platform abstraction for loading a DLL or shared object
        private static IntPtr GetDllHandle(string path)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return LoadLibrary(path);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            const int RTLD_NOW = 2;
            return dlopen(path, RTLD_NOW);
#else
            return IntPtr.Zero;
#endif
        }

        // Cross-platform abstraction for accessing a symbol within a DLL or shared object
        private static IntPtr GetDllExport(IntPtr dllHandle, string name)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return GetProcAddress(dllHandle, name);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            return dlsym(dllHandle, name);
#else
            return IntPtr.Zero;
#endif
        }

        // Cross-platform abstraction for freeing/closing a DLL or shared object
        private static bool FreeDllHandle(IntPtr dllHandle)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return FreeLibrary(dllHandle);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            return dlclose(_nativeLibraryPtr) == 0;
#else
            return false;
#endif
        }

        internal static void LoadMRUKSharedLibrary()
        {
            if (_nativeLibraryPtr != IntPtr.Zero)
            {
                return;
            }

            var path = string.Empty;
#if UNITY_EDITOR_WIN
            path = Path.GetFullPath("Packages/com.meta.xr.mrutilitykit/Plugins/Win64/mrutilitykitshared.dll");
#elif UNITY_EDITOR_OSX
            string folder = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "MacArm" : "Mac";
            path = Path.GetFullPath($"Packages/com.meta.xr.mrutilitykit/Plugins/{folder}/libmrutilitykitshared.dylib");
#elif UNITY_STANDALONE_WIN
            path = Path.Join(Application.dataPath, "Plugins/x86_64/mrutilitykitshared.dll");
#elif UNITY_STANDALONE_OSX
            // NOTE: This only works for Arm64 Macs
            path = Path.Join(Application.dataPath, "Plugins/ARM64/libmrutilitykitshared.dylib");
#elif UNITY_ANDROID
            path = "libmrutilitykitshared.so";
#else
            Debug.LogError($"mr utility kit shared library is not supported on this platform: '{Application.platform}'");
            return;
#endif
            _nativeLibraryPtr = GetDllHandle(path);

            if (_nativeLibraryPtr == IntPtr.Zero)
            {
                Debug.LogError($"Failed to load mr utility kit shared library from '{path}'");
            }
            else
            {
                MRUKNativeFuncs.LoadNativeFunctions();
            }
        }

        internal static void FreeMRUKSharedLibrary()
        {
            MRUKNativeFuncs.UnloadNativeFunctions();

            if (_nativeLibraryPtr == IntPtr.Zero)
            {
                return;
            }

            if (!FreeDllHandle(_nativeLibraryPtr))
            {
                Debug.LogError("Failed to free mr utility kit shared library");
            }

            _nativeLibraryPtr = IntPtr.Zero;
        }

        internal static T LoadFunction<T>(string name)
        {
            if (_nativeLibraryPtr == IntPtr.Zero)
            {
                Debug.LogWarning($"Failed to load {name} because mr utility kit shared library is not loaded");
                return default;
            }
            IntPtr funcPtr = GetDllExport(_nativeLibraryPtr, name);
            if (funcPtr == IntPtr.Zero)
            {
                Debug.LogWarning($"Could not find {name} in mr utility kit shared library");
                return default;
            }
            return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
        }
    }
}
