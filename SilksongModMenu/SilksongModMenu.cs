// SilksongModMenu.cs
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ModUINamespace
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public partial class SilksongModMenu : BaseUnityPlugin
    {
        public static class PluginInfo
        {
            public const string GUID = "com.silksong.modmenu";
            public const string Name = "Silksong Mod Menu";
            public const string Version = "1.0.0";
            public const string Author = "dringqian";
            public const string Website = "https://github.com/dreamstring/SilksongModMenu";
        }

        // ========== 添加这个常量 ==========
        private static readonly HashSet<string> IgnoredGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PluginInfo.GUID,  // 使用实际的 GUID
            "com.silksong.modmenu",
            "com.yourname.silksongmodmenu"  // 保留以防万一
        };

        public static readonly HashSet<string> IgnoredDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SilksongModMenu.dll",
            "ModPostProcessor.exe",
            "RestartStub.exe",
            "Newtonsoft.Json.dll",
            "0Harmony.dll"
        };

        public static SilksongModMenu Instance { get; private set; }
        public static MenuScreen modOptionsMenuScreen;
        public static bool isShowingModMenu = false;
        internal new static ManualLogSource Logger;

        private static MenuButton modOptionsButton = null;
        private static Transform modsContentParent;
        private static GameObject rowPrototype;
        private static Dictionary<string, bool> rowAdjustmentStatus = new Dictionary<string, bool>();

        private void Awake()
        {
            Instance = this; // 保存实例引用
            Logger = base.Logger;
            Logger.LogInfo("Silksong Mod Menu loaded.");

            var harmony = new Harmony("com.yourname.silksongmodmenu");
            harmony.PatchAll(typeof(SilksongModMenu));

            EnsureConfigLoaded();

            // 注册退出事件
            Application.quitting += OnApplicationQuitting;
        }

        private void OnDestroy()
        {
            // 清理事件订阅
            Application.quitting -= OnApplicationQuitting;
            Instance = null;
        }

        private void Update()
        {
            // 只在显示 Mod 菜单时监听
            if (!isShowingModMenu || modOptionsMenuScreen == null || !modOptionsMenuScreen.gameObject.activeSelf)
                return;

            // 直接监听键盘/手柄输入
            bool cancelPressed = Input.GetKeyDown(KeyCode.Escape) ||
                                 Input.GetKeyDown(KeyCode.X) ||
                                 Input.GetKeyDown(KeyCode.Backspace) ||
                                 Input.GetButtonDown("Cancel"); // Unity 默认的取消按钮

            if (cancelPressed)
            {
                Logger.LogInfo("[Update] Cancel key pressed in Mod menu!");

                var uiManager = FindObjectOfType<UIManager>();
                if (uiManager != null)
                {
                    StartCoroutine(ReturnToExtrasMenuCoroutine(uiManager));
                }
            }
        }


        /// <summary>
        /// 游戏退出时调用（包括正常退出和 Alt+F4）
        /// </summary>
        private void OnApplicationQuitting()
        {
            Logger.LogInfo("[Exit Hook] Game is quitting, processing mod changes...");

            try
            {
                // 直接调用 GameRestartUtil 的处理逻辑
                GameRestartUtil.ProcessModChangesOnExit();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[Exit Hook] Failed to process mod changes: {ex}");
            }
        }

        public static IEnumerator ReturnToExtrasMenuCoroutine(UIManager uiManager)
        {
            Logger.LogInfo("=== ReturnToExtrasMenuCoroutine START ===");
            Logger.LogInfo($"uiManager: {uiManager != null}");
            Logger.LogInfo($"modOptionsMenuScreen: {modOptionsMenuScreen != null}");
            Logger.LogInfo($"extrasMenuScreen: {uiManager?.extrasMenuScreen != null}");

            // 1. 停止输入处理
            var inputHandler = Traverse.Create(uiManager).Field("ih").GetValue<InputHandler>();
            if (inputHandler != null)
            {
                Logger.LogInfo("Stopping UI input...");
                inputHandler.StopUIInput();
            }

            // 2. 隐藏 Mod Options 菜单
            if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject.activeSelf)
            {
                Logger.LogInfo("Hiding Mod Options menu...");
                yield return uiManager.HideMenu(modOptionsMenuScreen);
                Logger.LogInfo("Mod Options menu hidden");
            }

            // 3. 重置状态
            isShowingModMenu = false;
            Logger.LogInfo("isShowingModMenu set to false");

            // 4. 显示 Extras 菜单
            if (uiManager.extrasMenuScreen != null)
            {
                Logger.LogInfo("Showing Extras menu...");
                yield return uiManager.ShowMenu(uiManager.extrasMenuScreen);

                // 恢复 CanvasGroup 交互
                var canvasGroup = uiManager.extrasMenuScreen.ScreenCanvasGroup;
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    canvasGroup.alpha = 1f;
                    Logger.LogInfo("Extras menu CanvasGroup restored");
                }

                // 恢复默认高亮
                if (uiManager.extrasMenuScreen.defaultHighlight != null)
                {
                    Logger.LogInfo("Selecting default highlight...");
                    uiManager.extrasMenuScreen.defaultHighlight.Select();
                }
                else
                {
                    Logger.LogInfo("Highlighting default...");
                    uiManager.extrasMenuScreen.HighlightDefault();
                }

                Logger.LogInfo("Extras menu shown");
            }
            else
            {
                Logger.LogError("extrasMenuScreen is null!");
            }

            // 5. 恢复输入处理
            if (inputHandler != null)
            {
                Logger.LogInfo("Starting UI input...");
                inputHandler.StartUIInput();
            }

            Logger.LogInfo("=== ReturnToExtrasMenuCoroutine END ===");
        }

    }

    [HarmonyPatch]
    public static class DebugAllMenuMethods
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            // 尝试 Patch 所有可能的方法
            var methods = new List<MethodBase>();

            // MenuScreen 的方法
            var menuScreenType = typeof(MenuScreen);
            var goBack = AccessTools.Method(menuScreenType, "GoBack");
            if (goBack != null) methods.Add(goBack);

            var hide = AccessTools.Method(menuScreenType, "Hide");
            if (hide != null) methods.Add(hide);

            // UIManager 的方法
            var uiManagerType = typeof(UIManager);
            foreach (var method in uiManagerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name.Contains("Back") || method.Name.Contains("Cancel") || method.Name.Contains("Close"))
                {
                    methods.Add(method);
                }
            }

            return methods;
        }

        static void Prefix(object __instance, MethodBase __originalMethod)
        {
            if (SilksongModMenu.isShowingModMenu)
            {
                SilksongModMenu.Logger.LogInfo($"[DEBUG] ⚠️ {__originalMethod.DeclaringType.Name}.{__originalMethod.Name} called while in Mod menu!");
                SilksongModMenu.Logger.LogInfo($"[DEBUG] Instance: {__instance}");
                SilksongModMenu.Logger.LogInfo($"[DEBUG] Stack trace: {Environment.StackTrace}");
            }
        }
    }

    // MenuScreen.GoBack Patch（保留这个）
    [HarmonyPatch(typeof(MenuScreen), "GoBack")]
    public static class MenuScreen_GoBack_Patch
    {
        static bool Prefix(MenuScreen __instance, ref bool __result)
        {
            if (__instance == SilksongModMenu.modOptionsMenuScreen)
            {
                SilksongModMenu.Logger.LogInfo("[GoBack Patch] Intercepted! Returning to Extras menu...");

                var uiManager = GameObject.FindObjectOfType<UIManager>();
                if (uiManager != null)
                {
                    uiManager.StartCoroutine(SilksongModMenu.ReturnToExtrasMenuCoroutine(uiManager));
                }

                __result = true;
                return false;
            }

            return true;
        }
    }
}
