using System.Runtime.CompilerServices;
using UnityEngine;

#if USING_META_CORE_SDK_77_OR_NEWER && UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("Oculus.Platform.Editor")]
namespace Oculus.Platform
{
    internal class EventManager
    {
        private static string projectName;
        private static string projectGUID;

        internal static void SendUnifiedEvent(
            bool isEssential,
            string productType,
            string eventName,
            string event_metadata_json,
            string event_entrypoint = "",
            string event_type = "",
            string event_target = "",
            string error_msg = "",
            string is_internal = "")
        {
#if USING_META_CORE_SDK_77_OR_NEWER && UNITY_EDITOR
            OVRPlugin.Bool ovrBool = isEssential ? OVRPlugin.Bool.True : OVRPlugin.Bool.False;

            if (string.IsNullOrEmpty(projectName)) {
                projectName = PlayerSettings.productName;
            }

            if (string.IsNullOrEmpty(projectGUID))
            {
                projectGUID = PlayerSettings.productGUID.ToString();
            }

            OVRPlugin.SendUnifiedEvent(
                ovrBool,
                productType,
                eventName,
                event_metadata_json,
                projectName,
                event_entrypoint,
                projectGUID,
                event_type,
                event_target,
                error_msg,
                is_internal
            );
#endif
        }
    }
}
