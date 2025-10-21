extern "C" void RegisterStaticallyLinkedModulesGranular()
{
	void RegisterModule_SharedInternals();
	RegisterModule_SharedInternals();

	void RegisterModule_Core();
	RegisterModule_Core();

	void RegisterModule_AI();
	RegisterModule_AI();

	void RegisterModule_AndroidJNI();
	RegisterModule_AndroidJNI();

	void RegisterModule_Animation();
	RegisterModule_Animation();

	void RegisterModule_AssetBundle();
	RegisterModule_AssetBundle();

	void RegisterModule_Audio();
	RegisterModule_Audio();

	void RegisterModule_GraphicsStateCollectionSerializer();
	RegisterModule_GraphicsStateCollectionSerializer();

	void RegisterModule_HierarchyCore();
	RegisterModule_HierarchyCore();

	void RegisterModule_HotReload();
	RegisterModule_HotReload();

	void RegisterModule_ImageConversion();
	RegisterModule_ImageConversion();

	void RegisterModule_Input();
	RegisterModule_Input();

	void RegisterModule_InputLegacy();
	RegisterModule_InputLegacy();

	void RegisterModule_IMGUI();
	RegisterModule_IMGUI();

	void RegisterModule_InputForUI();
	RegisterModule_InputForUI();

	void RegisterModule_JSONSerialize();
	RegisterModule_JSONSerialize();

	void RegisterModule_ParticleSystem();
	RegisterModule_ParticleSystem();

	void RegisterModule_Physics();
	RegisterModule_Physics();

	void RegisterModule_Physics2D();
	RegisterModule_Physics2D();

	void RegisterModule_Properties();
	RegisterModule_Properties();

	void RegisterModule_RuntimeInitializeOnLoadManagerInitializer();
	RegisterModule_RuntimeInitializeOnLoadManagerInitializer();

	void RegisterModule_Subsystems();
	RegisterModule_Subsystems();

	void RegisterModule_TLS();
	RegisterModule_TLS();

	void RegisterModule_Terrain();
	RegisterModule_Terrain();

	void RegisterModule_TextRendering();
	RegisterModule_TextRendering();

	void RegisterModule_TextCoreFontEngine();
	RegisterModule_TextCoreFontEngine();

	void RegisterModule_TextCoreTextEngine();
	RegisterModule_TextCoreTextEngine();

	void RegisterModule_UI();
	RegisterModule_UI();

	void RegisterModule_UIElements();
	RegisterModule_UIElements();

	void RegisterModule_UnityAnalyticsCommon();
	RegisterModule_UnityAnalyticsCommon();

	void RegisterModule_UnityConnect();
	RegisterModule_UnityConnect();

	void RegisterModule_UnityWebRequest();
	RegisterModule_UnityWebRequest();

	void RegisterModule_UnityAnalytics();
	RegisterModule_UnityAnalytics();

	void RegisterModule_VFX();
	RegisterModule_VFX();

	void RegisterModule_VR();
	RegisterModule_VR();

	void RegisterModule_Video();
	RegisterModule_Video();

	void RegisterModule_XR();
	RegisterModule_XR();

}

template <typename T> void RegisterUnityClass(const char*);
template <typename T> void RegisterStrippedType(int, const char*, const char*);

void InvokeRegisterStaticallyLinkedModuleClasses()
{
	// Do nothing (we're in stripping mode)
}

class NavMeshAgent; template <> void RegisterUnityClass<NavMeshAgent>(const char*);
class NavMeshData; template <> void RegisterUnityClass<NavMeshData>(const char*);
class NavMeshObstacle; template <> void RegisterUnityClass<NavMeshObstacle>(const char*);
class NavMeshProjectSettings; template <> void RegisterUnityClass<NavMeshProjectSettings>(const char*);
class NavMeshSettings; template <> void RegisterUnityClass<NavMeshSettings>(const char*);
class AnimationClip; template <> void RegisterUnityClass<AnimationClip>(const char*);
class Animator; template <> void RegisterUnityClass<Animator>(const char*);
class AnimatorController; template <> void RegisterUnityClass<AnimatorController>(const char*);
class AnimatorOverrideController; template <> void RegisterUnityClass<AnimatorOverrideController>(const char*);
class Avatar; template <> void RegisterUnityClass<Avatar>(const char*);
class IConstraint; template <> void RegisterUnityClass<IConstraint>(const char*);
class Motion; template <> void RegisterUnityClass<Motion>(const char*);
class PositionConstraint; template <> void RegisterUnityClass<PositionConstraint>(const char*);
class RuntimeAnimatorController; template <> void RegisterUnityClass<RuntimeAnimatorController>(const char*);
class AssetBundle; template <> void RegisterUnityClass<AssetBundle>(const char*);
class AudioBehaviour; template <> void RegisterUnityClass<AudioBehaviour>(const char*);
class AudioClip; template <> void RegisterUnityClass<AudioClip>(const char*);
class AudioListener; template <> void RegisterUnityClass<AudioListener>(const char*);
class AudioManager; template <> void RegisterUnityClass<AudioManager>(const char*);
class AudioResource; template <> void RegisterUnityClass<AudioResource>(const char*);
class AudioSource; template <> void RegisterUnityClass<AudioSource>(const char*);
class BaseVideoTexture; template <> void RegisterUnityClass<BaseVideoTexture>(const char*);
class SampleClip; template <> void RegisterUnityClass<SampleClip>(const char*);
class WebCamTexture; template <> void RegisterUnityClass<WebCamTexture>(const char*);
class Behaviour; template <> void RegisterUnityClass<Behaviour>(const char*);
class BuildSettings; template <> void RegisterUnityClass<BuildSettings>(const char*);
class Camera; template <> void RegisterUnityClass<Camera>(const char*);
namespace Unity { class Component; } template <> void RegisterUnityClass<Unity::Component>(const char*);
class ComputeShader; template <> void RegisterUnityClass<ComputeShader>(const char*);
class Cubemap; template <> void RegisterUnityClass<Cubemap>(const char*);
class CubemapArray; template <> void RegisterUnityClass<CubemapArray>(const char*);
class DelayedCallManager; template <> void RegisterUnityClass<DelayedCallManager>(const char*);
class EditorExtension; template <> void RegisterUnityClass<EditorExtension>(const char*);
class GameManager; template <> void RegisterUnityClass<GameManager>(const char*);
class GameObject; template <> void RegisterUnityClass<GameObject>(const char*);
class GlobalGameManager; template <> void RegisterUnityClass<GlobalGameManager>(const char*);
class GraphicsSettings; template <> void RegisterUnityClass<GraphicsSettings>(const char*);
class InputManager; template <> void RegisterUnityClass<InputManager>(const char*);
class LODGroup; template <> void RegisterUnityClass<LODGroup>(const char*);
class LevelGameManager; template <> void RegisterUnityClass<LevelGameManager>(const char*);
class Light; template <> void RegisterUnityClass<Light>(const char*);
class LightProbeProxyVolume; template <> void RegisterUnityClass<LightProbeProxyVolume>(const char*);
class LightProbes; template <> void RegisterUnityClass<LightProbes>(const char*);
class LightingSettings; template <> void RegisterUnityClass<LightingSettings>(const char*);
class LightmapSettings; template <> void RegisterUnityClass<LightmapSettings>(const char*);
class LineRenderer; template <> void RegisterUnityClass<LineRenderer>(const char*);
class LowerResBlitTexture; template <> void RegisterUnityClass<LowerResBlitTexture>(const char*);
class Material; template <> void RegisterUnityClass<Material>(const char*);
class Mesh; template <> void RegisterUnityClass<Mesh>(const char*);
class MeshFilter; template <> void RegisterUnityClass<MeshFilter>(const char*);
class MeshRenderer; template <> void RegisterUnityClass<MeshRenderer>(const char*);
class MonoBehaviour; template <> void RegisterUnityClass<MonoBehaviour>(const char*);
class MonoManager; template <> void RegisterUnityClass<MonoManager>(const char*);
class MonoScript; template <> void RegisterUnityClass<MonoScript>(const char*);
class NamedObject; template <> void RegisterUnityClass<NamedObject>(const char*);
class Object; template <> void RegisterUnityClass<Object>(const char*);
class PlayerSettings; template <> void RegisterUnityClass<PlayerSettings>(const char*);
class PreloadData; template <> void RegisterUnityClass<PreloadData>(const char*);
class QualitySettings; template <> void RegisterUnityClass<QualitySettings>(const char*);
class RayTracingShader; template <> void RegisterUnityClass<RayTracingShader>(const char*);
namespace UI { class RectTransform; } template <> void RegisterUnityClass<UI::RectTransform>(const char*);
class ReflectionProbe; template <> void RegisterUnityClass<ReflectionProbe>(const char*);
class RenderSettings; template <> void RegisterUnityClass<RenderSettings>(const char*);
class RenderTexture; template <> void RegisterUnityClass<RenderTexture>(const char*);
class Renderer; template <> void RegisterUnityClass<Renderer>(const char*);
class ResourceManager; template <> void RegisterUnityClass<ResourceManager>(const char*);
class RuntimeInitializeOnLoadManager; template <> void RegisterUnityClass<RuntimeInitializeOnLoadManager>(const char*);
class Shader; template <> void RegisterUnityClass<Shader>(const char*);
class ShaderNameRegistry; template <> void RegisterUnityClass<ShaderNameRegistry>(const char*);
class SkinnedMeshRenderer; template <> void RegisterUnityClass<SkinnedMeshRenderer>(const char*);
class Skybox; template <> void RegisterUnityClass<Skybox>(const char*);
class SortingGroup; template <> void RegisterUnityClass<SortingGroup>(const char*);
class Sprite; template <> void RegisterUnityClass<Sprite>(const char*);
class SpriteAtlas; template <> void RegisterUnityClass<SpriteAtlas>(const char*);
class SpriteRenderer; template <> void RegisterUnityClass<SpriteRenderer>(const char*);
class TagManager; template <> void RegisterUnityClass<TagManager>(const char*);
class TextAsset; template <> void RegisterUnityClass<TextAsset>(const char*);
class Texture; template <> void RegisterUnityClass<Texture>(const char*);
class Texture2D; template <> void RegisterUnityClass<Texture2D>(const char*);
class Texture2DArray; template <> void RegisterUnityClass<Texture2DArray>(const char*);
class Texture3D; template <> void RegisterUnityClass<Texture3D>(const char*);
class TimeManager; template <> void RegisterUnityClass<TimeManager>(const char*);
class Transform; template <> void RegisterUnityClass<Transform>(const char*);
class ParticleSystem; template <> void RegisterUnityClass<ParticleSystem>(const char*);
class ParticleSystemRenderer; template <> void RegisterUnityClass<ParticleSystemRenderer>(const char*);
class BoxCollider; template <> void RegisterUnityClass<BoxCollider>(const char*);
class CapsuleCollider; template <> void RegisterUnityClass<CapsuleCollider>(const char*);
class CharacterController; template <> void RegisterUnityClass<CharacterController>(const char*);
class Collider; template <> void RegisterUnityClass<Collider>(const char*);
namespace Unity { class ConfigurableJoint; } template <> void RegisterUnityClass<Unity::ConfigurableJoint>(const char*);
namespace Unity { class FixedJoint; } template <> void RegisterUnityClass<Unity::FixedJoint>(const char*);
namespace Unity { class Joint; } template <> void RegisterUnityClass<Unity::Joint>(const char*);
class MeshCollider; template <> void RegisterUnityClass<MeshCollider>(const char*);
class PhysicsManager; template <> void RegisterUnityClass<PhysicsManager>(const char*);
class PhysicsMaterial; template <> void RegisterUnityClass<PhysicsMaterial>(const char*);
class Rigidbody; template <> void RegisterUnityClass<Rigidbody>(const char*);
class SphereCollider; template <> void RegisterUnityClass<SphereCollider>(const char*);
class Collider2D; template <> void RegisterUnityClass<Collider2D>(const char*);
class Joint2D; template <> void RegisterUnityClass<Joint2D>(const char*);
class Physics2DSettings; template <> void RegisterUnityClass<Physics2DSettings>(const char*);
class Rigidbody2D; template <> void RegisterUnityClass<Rigidbody2D>(const char*);
class Terrain; template <> void RegisterUnityClass<Terrain>(const char*);
class TerrainData; template <> void RegisterUnityClass<TerrainData>(const char*);
namespace TextRendering { class Font; } template <> void RegisterUnityClass<TextRendering::Font>(const char*);
namespace UI { class Canvas; } template <> void RegisterUnityClass<UI::Canvas>(const char*);
namespace UI { class CanvasGroup; } template <> void RegisterUnityClass<UI::CanvasGroup>(const char*);
namespace UI { class CanvasRenderer; } template <> void RegisterUnityClass<UI::CanvasRenderer>(const char*);
class UIRenderer; template <> void RegisterUnityClass<UIRenderer>(const char*);
class VFXManager; template <> void RegisterUnityClass<VFXManager>(const char*);
class VFXRenderer; template <> void RegisterUnityClass<VFXRenderer>(const char*);
class VisualEffect; template <> void RegisterUnityClass<VisualEffect>(const char*);
class VisualEffectAsset; template <> void RegisterUnityClass<VisualEffectAsset>(const char*);
class VisualEffectObject; template <> void RegisterUnityClass<VisualEffectObject>(const char*);
class VideoPlayer; template <> void RegisterUnityClass<VideoPlayer>(const char*);

void RegisterAllClasses()
{
void RegisterBuiltinTypes();
RegisterBuiltinTypes();
	//Total: 114 non stripped classes
	//0. NavMeshAgent
	RegisterUnityClass<NavMeshAgent>("AI");
	//1. NavMeshData
	RegisterUnityClass<NavMeshData>("AI");
	//2. NavMeshObstacle
	RegisterUnityClass<NavMeshObstacle>("AI");
	//3. NavMeshProjectSettings
	RegisterUnityClass<NavMeshProjectSettings>("AI");
	//4. NavMeshSettings
	RegisterUnityClass<NavMeshSettings>("AI");
	//5. AnimationClip
	RegisterUnityClass<AnimationClip>("Animation");
	//6. Animator
	RegisterUnityClass<Animator>("Animation");
	//7. AnimatorController
	RegisterUnityClass<AnimatorController>("Animation");
	//8. AnimatorOverrideController
	RegisterUnityClass<AnimatorOverrideController>("Animation");
	//9. Avatar
	RegisterUnityClass<Avatar>("Animation");
	//10. IConstraint
	RegisterUnityClass<IConstraint>("Animation");
	//11. Motion
	RegisterUnityClass<Motion>("Animation");
	//12. PositionConstraint
	RegisterUnityClass<PositionConstraint>("Animation");
	//13. RuntimeAnimatorController
	RegisterUnityClass<RuntimeAnimatorController>("Animation");
	//14. AssetBundle
	RegisterUnityClass<AssetBundle>("AssetBundle");
	//15. AudioBehaviour
	RegisterUnityClass<AudioBehaviour>("Audio");
	//16. AudioClip
	RegisterUnityClass<AudioClip>("Audio");
	//17. AudioListener
	RegisterUnityClass<AudioListener>("Audio");
	//18. AudioManager
	RegisterUnityClass<AudioManager>("Audio");
	//19. AudioResource
	RegisterUnityClass<AudioResource>("Audio");
	//20. AudioSource
	RegisterUnityClass<AudioSource>("Audio");
	//21. BaseVideoTexture
	RegisterUnityClass<BaseVideoTexture>("Audio");
	//22. SampleClip
	RegisterUnityClass<SampleClip>("Audio");
	//23. WebCamTexture
	RegisterUnityClass<WebCamTexture>("Audio");
	//24. Behaviour
	RegisterUnityClass<Behaviour>("Core");
	//25. BuildSettings
	RegisterUnityClass<BuildSettings>("Core");
	//26. Camera
	RegisterUnityClass<Camera>("Core");
	//27. Component
	RegisterUnityClass<Unity::Component>("Core");
	//28. ComputeShader
	RegisterUnityClass<ComputeShader>("Core");
	//29. Cubemap
	RegisterUnityClass<Cubemap>("Core");
	//30. CubemapArray
	RegisterUnityClass<CubemapArray>("Core");
	//31. DelayedCallManager
	RegisterUnityClass<DelayedCallManager>("Core");
	//32. EditorExtension
	RegisterUnityClass<EditorExtension>("Core");
	//33. GameManager
	RegisterUnityClass<GameManager>("Core");
	//34. GameObject
	RegisterUnityClass<GameObject>("Core");
	//35. GlobalGameManager
	RegisterUnityClass<GlobalGameManager>("Core");
	//36. GraphicsSettings
	RegisterUnityClass<GraphicsSettings>("Core");
	//37. InputManager
	RegisterUnityClass<InputManager>("Core");
	//38. LODGroup
	RegisterUnityClass<LODGroup>("Core");
	//39. LevelGameManager
	RegisterUnityClass<LevelGameManager>("Core");
	//40. Light
	RegisterUnityClass<Light>("Core");
	//41. LightProbeProxyVolume
	RegisterUnityClass<LightProbeProxyVolume>("Core");
	//42. LightProbes
	RegisterUnityClass<LightProbes>("Core");
	//43. LightingSettings
	RegisterUnityClass<LightingSettings>("Core");
	//44. LightmapSettings
	RegisterUnityClass<LightmapSettings>("Core");
	//45. LineRenderer
	RegisterUnityClass<LineRenderer>("Core");
	//46. LowerResBlitTexture
	RegisterUnityClass<LowerResBlitTexture>("Core");
	//47. Material
	RegisterUnityClass<Material>("Core");
	//48. Mesh
	RegisterUnityClass<Mesh>("Core");
	//49. MeshFilter
	RegisterUnityClass<MeshFilter>("Core");
	//50. MeshRenderer
	RegisterUnityClass<MeshRenderer>("Core");
	//51. MonoBehaviour
	RegisterUnityClass<MonoBehaviour>("Core");
	//52. MonoManager
	RegisterUnityClass<MonoManager>("Core");
	//53. MonoScript
	RegisterUnityClass<MonoScript>("Core");
	//54. NamedObject
	RegisterUnityClass<NamedObject>("Core");
	//55. Object
	//Skipping Object
	//56. PlayerSettings
	RegisterUnityClass<PlayerSettings>("Core");
	//57. PreloadData
	RegisterUnityClass<PreloadData>("Core");
	//58. QualitySettings
	RegisterUnityClass<QualitySettings>("Core");
	//59. RayTracingShader
	RegisterUnityClass<RayTracingShader>("Core");
	//60. RectTransform
	RegisterUnityClass<UI::RectTransform>("Core");
	//61. ReflectionProbe
	RegisterUnityClass<ReflectionProbe>("Core");
	//62. RenderSettings
	RegisterUnityClass<RenderSettings>("Core");
	//63. RenderTexture
	RegisterUnityClass<RenderTexture>("Core");
	//64. Renderer
	RegisterUnityClass<Renderer>("Core");
	//65. ResourceManager
	RegisterUnityClass<ResourceManager>("Core");
	//66. RuntimeInitializeOnLoadManager
	RegisterUnityClass<RuntimeInitializeOnLoadManager>("Core");
	//67. Shader
	RegisterUnityClass<Shader>("Core");
	//68. ShaderNameRegistry
	RegisterUnityClass<ShaderNameRegistry>("Core");
	//69. SkinnedMeshRenderer
	RegisterUnityClass<SkinnedMeshRenderer>("Core");
	//70. Skybox
	RegisterUnityClass<Skybox>("Core");
	//71. SortingGroup
	RegisterUnityClass<SortingGroup>("Core");
	//72. Sprite
	RegisterUnityClass<Sprite>("Core");
	//73. SpriteAtlas
	RegisterUnityClass<SpriteAtlas>("Core");
	//74. SpriteRenderer
	RegisterUnityClass<SpriteRenderer>("Core");
	//75. TagManager
	RegisterUnityClass<TagManager>("Core");
	//76. TextAsset
	RegisterUnityClass<TextAsset>("Core");
	//77. Texture
	RegisterUnityClass<Texture>("Core");
	//78. Texture2D
	RegisterUnityClass<Texture2D>("Core");
	//79. Texture2DArray
	RegisterUnityClass<Texture2DArray>("Core");
	//80. Texture3D
	RegisterUnityClass<Texture3D>("Core");
	//81. TimeManager
	RegisterUnityClass<TimeManager>("Core");
	//82. Transform
	RegisterUnityClass<Transform>("Core");
	//83. ParticleSystem
	RegisterUnityClass<ParticleSystem>("ParticleSystem");
	//84. ParticleSystemRenderer
	RegisterUnityClass<ParticleSystemRenderer>("ParticleSystem");
	//85. BoxCollider
	RegisterUnityClass<BoxCollider>("Physics");
	//86. CapsuleCollider
	RegisterUnityClass<CapsuleCollider>("Physics");
	//87. CharacterController
	RegisterUnityClass<CharacterController>("Physics");
	//88. Collider
	RegisterUnityClass<Collider>("Physics");
	//89. ConfigurableJoint
	RegisterUnityClass<Unity::ConfigurableJoint>("Physics");
	//90. FixedJoint
	RegisterUnityClass<Unity::FixedJoint>("Physics");
	//91. Joint
	RegisterUnityClass<Unity::Joint>("Physics");
	//92. MeshCollider
	RegisterUnityClass<MeshCollider>("Physics");
	//93. PhysicsManager
	RegisterUnityClass<PhysicsManager>("Physics");
	//94. PhysicsMaterial
	RegisterUnityClass<PhysicsMaterial>("Physics");
	//95. Rigidbody
	RegisterUnityClass<Rigidbody>("Physics");
	//96. SphereCollider
	RegisterUnityClass<SphereCollider>("Physics");
	//97. Collider2D
	RegisterUnityClass<Collider2D>("Physics2D");
	//98. Joint2D
	RegisterUnityClass<Joint2D>("Physics2D");
	//99. Physics2DSettings
	RegisterUnityClass<Physics2DSettings>("Physics2D");
	//100. Rigidbody2D
	RegisterUnityClass<Rigidbody2D>("Physics2D");
	//101. Terrain
	RegisterUnityClass<Terrain>("Terrain");
	//102. TerrainData
	RegisterUnityClass<TerrainData>("Terrain");
	//103. Font
	RegisterUnityClass<TextRendering::Font>("TextRendering");
	//104. Canvas
	RegisterUnityClass<UI::Canvas>("UI");
	//105. CanvasGroup
	RegisterUnityClass<UI::CanvasGroup>("UI");
	//106. CanvasRenderer
	RegisterUnityClass<UI::CanvasRenderer>("UI");
	//107. UIRenderer
	RegisterUnityClass<UIRenderer>("UIElements");
	//108. VFXManager
	RegisterUnityClass<VFXManager>("VFX");
	//109. VFXRenderer
	RegisterUnityClass<VFXRenderer>("VFX");
	//110. VisualEffect
	RegisterUnityClass<VisualEffect>("VFX");
	//111. VisualEffectAsset
	RegisterUnityClass<VisualEffectAsset>("VFX");
	//112. VisualEffectObject
	RegisterUnityClass<VisualEffectObject>("VFX");
	//113. VideoPlayer
	RegisterUnityClass<VideoPlayer>("Video");

}
