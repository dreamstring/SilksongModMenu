// SilksongModMenu.cs
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ModUINamespace
{
    [BepInPlugin("com.yourname.silksongmodmenu", "Silksong Mod Menu", "1.0.0")]
    public partial class SilksongModMenu : BaseUnityPlugin
    {
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
            harmony.PatchAll(typeof(UIManager_GoToPreviousMenu_Patch));

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

    [HarmonyPatch(typeof(UIManager), "GoToPreviousMenu")]
    public static class UIManager_GoToPreviousMenu_Patch
    {
        static bool Prefix(UIManager __instance)
        {
            // 添加详细日志
            SilksongModMenu.Logger.LogInfo($"[GoToPreviousMenu] Called. isShowingModMenu={SilksongModMenu.isShowingModMenu}, modOptionsMenuScreen exists={SilksongModMenu.modOptionsMenuScreen != null}");

            if (SilksongModMenu.isShowingModMenu && SilksongModMenu.modOptionsMenuScreen != null)
            {
                SilksongModMenu.Logger.LogInfo("[X Button] Intercepted - closing Mod menu");

                try
                {
                    // 优先从 Plugin 实例启动 Coroutine
                    if (SilksongModMenu.Instance != null)
                    {
                        SilksongModMenu.Instance.StartCoroutine(
                            SilksongModMenu.ReturnToExtrasMenuCoroutine(__instance)
                        );
                        SilksongModMenu.Logger.LogInfo("[X Button] Coroutine started from Plugin instance");
                    }
                    else
                    {
                        // 备用方案：从 UIManager 实例启动
                        __instance.StartCoroutine(SilksongModMenu.ReturnToExtrasMenuCoroutine(__instance));
                        SilksongModMenu.Logger.LogInfo("[X Button] Coroutine started from UIManager instance");
                    }
                }
                catch (System.Exception ex)
                {
                    SilksongModMenu.Logger.LogError($"[X Button] Failed to start coroutine: {ex}");
                    // 如果启动 Coroutine 失败，允许原方法执行（避免卡死）
                    return true;
                }

                return false; // 阻止原方法执行
            }

            SilksongModMenu.Logger.LogInfo("[GoToPreviousMenu] Allowing original method to execute");
            return true; // 允许原方法执行
        }
    }
}
