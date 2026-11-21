// Controlled Unity API substitute around the EXACT production component.
// Tests event-driven silhouette residency without requiring Unity Editor.
// Native Unity render/visibility, occlusion and APK testing remain mandatory.
using System;
using System.Collections.Generic;
using System.Reflection;
using Aaris.MapReview;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }
    public class MonoBehaviour { }
    public sealed class Renderer
    {
        public bool enabled = true;
    }
}

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Additive }
    public struct Scene
    {
        public string path;
        public bool isLoaded;
        public bool IsValid() { return path != null; }
    }
    public static class SceneManager
    {
        public static readonly Dictionary<string, bool> loaded =
            new Dictionary<string, bool>();
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static event Action<Scene> sceneUnloaded;

        public static Scene GetSceneByPath(string path)
        {
            bool present = loaded.ContainsKey(path);
            return new Scene { path = present ? path : null,
                               isLoaded = present && loaded[path] };
        }
        public static void RaiseLoad(string path)
        {
            loaded[path] = true;
            if (sceneLoaded != null)
                sceneLoaded(new Scene { path = path, isLoaded = true },
                            LoadSceneMode.Additive);
        }
        public static void RaiseUnload(string path)
        {
            loaded[path] = false;
            if (sceneUnloaded != null)
                sceneUnloaded(new Scene { path = path, isLoaded = false });
        }
        public static void RaiseUnloadWithoutStablePath(string path)
        {
            loaded[path] = false;
            // Simulate native tear-down making the event Scene.path invalid.
            if (sceneUnloaded != null)
                sceneUnloaded(new Scene { path = null, isLoaded = false });
        }
        public static void Reset()
        {
            loaded.Clear();
            sceneLoaded = null;
            sceneUnloaded = null;
        }
    }
}

internal static class WorldChunkFallbackVisibilityTests
{
    private static int checks;
    private static void Assert(bool value, string reason)
    {
        checks++;
        if (!value) throw new Exception(reason);
    }
    private static void Set(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new Exception("Missing production field " + name);
        field.SetValue(instance, value);
    }
    private static void Call(object instance, string name)
    {
        MethodInfo method = instance.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new Exception("Missing lifecycle " + name);
        method.Invoke(instance, null);
    }
    public static int Main()
    {
        const string target = "Assets/AarisWorld/Scenes/Chunks/Aaris_ForestLookout_Detail.unity";
        const string other = "Assets/Other_Detail.unity";

        SceneManager.Reset();
        var originalFar = new Renderer();
        var secondFar = new Renderer();
        var c = new AarisChunkFallbackVisibility();
        Set(c, "chunkScenePath", target);
        Set(c, "silhouetteRenderers", new [] { originalFar, secondFar, null });
        Call(c, "OnEnable");
        Assert(originalFar.enabled && secondFar.enabled, "Unloaded chunk has no silhouette");

        SceneManager.RaiseLoad(other);
        Assert(originalFar.enabled && secondFar.enabled, "Unrelated scene affected silhouette");
        SceneManager.RaiseLoad(target);
        Assert(!originalFar.enabled && !secondFar.enabled,
               "Loaded high-detail chunk duplicated distant LOD");
        SceneManager.RaiseUnload(other);
        Assert(!originalFar.enabled && !secondFar.enabled,
               "Unrelated unload changed full-detail visibility");
        SceneManager.RaiseUnload(target);
        Assert(originalFar.enabled && secondFar.enabled,
               "Scene unloading left a distant silhouette hole");

        SceneManager.RaiseLoad(target);
        Assert(!originalFar.enabled && !secondFar.enabled,
               "Full detail was not restored on reload");
        SceneManager.RaiseUnloadWithoutStablePath(target);
        Assert(originalFar.enabled && secondFar.enabled,
               "Native unload invalidated Scene.path and hid last-LOD silhouette");

        SceneManager.RaiseLoad(target);
        Call(c, "OnDisable");
        Assert(originalFar.enabled && secondFar.enabled,
               "Disabling pilot manager erased original distant landmark");
        SceneManager.RaiseUnload(target);
        SceneManager.RaiseLoad(target);
        Assert(originalFar.enabled, "Disabled manager still subscribed to scene events");

        Call(c, "OnEnable");
        Assert(!originalFar.enabled && !secondFar.enabled,
               "Re-enabled manager failed to detect already loaded chunk");
        SceneManager.RaiseUnload(target);
        Assert(originalFar.enabled && secondFar.enabled,
               "Re-enabled manager missed unload event");
        Call(c, "OnDisable");

        SceneManager.Reset();
        c = new AarisChunkFallbackVisibility();
        var emergencySilhouette = new Renderer { enabled = false };
        Set(c, "silhouetteRenderers", new [] { emergencySilhouette });
        Call(c, "OnEnable");
        Assert(emergencySilhouette.enabled, "Missing scene path must fail visibly, not invisibly");
        Call(c, "OnDisable");

        SceneManager.Reset();
        SceneManager.loaded[target] = true;
        c = new AarisChunkFallbackVisibility();
        var alreadyLoaded = new Renderer();
        Set(c, "chunkScenePath", target);
        Set(c, "silhouetteRenderers", new [] { alreadyLoaded });
        Call(c, "OnEnable");
        Assert(!alreadyLoaded.enabled, "Cold enable duplicated a resident full-detail chunk");
        Call(c, "OnDisable");
        Assert(alreadyLoaded.enabled, "Disable must release distant renderer");

        Console.WriteLine("WORLD_CHUNK_FALLBACK_RUNTIME_PASS checks=" + checks);
        Console.WriteLine("Controlled API fixture only; no native Unity FPS or render screenshot claim.");
        return 0;
    }
}
