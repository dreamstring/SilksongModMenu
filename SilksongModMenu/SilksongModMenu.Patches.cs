// SilksongModMenu.Patches.cs
using HarmonyLib;
using System;
using UnityEngine.UI;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "GoToExtrasMenu")]
        public static void AddModButtonToExtrasMenu(UIManager __instance)
        {
            Logger.LogInfo("=== GoToExtrasMenu called ===");

            try
            {
                // 首先检查是否已经存在按钮
                if (__instance.extrasMenuScreen != null)
                {
                    var existingButton = __instance.extrasMenuScreen.transform.Find("ModOptionsButton");
                    if (existingButton != null)
                    {
                        Logger.LogInfo("Mod Options button already exists, skipping creation");
                        existingButton.gameObject.SetActive(true);

                        // 更新静态引用
                        modOptionsButton = existingButton.GetComponent<MenuButton>();
                        return;
                    }
                }

                // 检查静态引用是否有效
                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Logger.LogInfo("Static reference exists and valid, skipping creation");
                    modOptionsButton.gameObject.SetActive(true);
                    return;
                }

                // 重置引用，准备创建新按钮
                modOptionsButton = null;
                __instance.StartCoroutine(AddModButtonCoroutine(__instance));
            }
            catch (Exception e)
            {
                Logger.LogError($"AddModButtonToExtrasMenu error: {e}");
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "Start")]
        public static void OnUIManagerStartPost(UIManager __instance)
        {
            try
            {
                // 检查这是否是有效的UIManager实例
                if (__instance != null && __instance == UIManager.instance)
                {
                    Logger.LogInfo("UIManager Start completed, resetting mod menu state");

                    // 只重置我们的状态，不影响游戏逻辑
                    ResetModMenuState();
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"OnUIManagerStartPost error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "OnDestroy")]
        public static void OnUIManagerDestroyPost(UIManager __instance)
        {
            try
            {
                Logger.LogInfo("UIManager OnDestroy completed, cleaning up mod menu");

                // 安全清理我们的组件
                SafeCleanupModMenu();
            }
            catch (Exception e)
            {
                Logger.LogError($"OnUIManagerDestroyPost error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIManager), "GoToExtrasContentMenu")]
        public static void OnReturnToExtrasMenu(UIManager __instance)
        {
            Logger.LogInfo("=== Returning to Extras Menu ===");
            __instance.StartCoroutine(EnsureModButtonExists(__instance));
        }
    }
}
