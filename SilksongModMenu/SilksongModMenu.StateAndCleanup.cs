// SilksongModMenu.StateAndCleanup.cs
using System;
using UnityEngine;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
        private static void ResetAllRowAdjustments()
        {
            rowAdjustmentStatus.Clear();
            Logger.LogInfo("Reset all row adjustment status");
        }

        private static void ResetModMenuState()
        {
            try
            {
                Logger.LogInfo("Resetting mod menu state...");

                // 检查现有引用是否还有效
                if (modOptionsButton != null && modOptionsButton.gameObject == null)
                {
                    modOptionsButton = null;
                }

                if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject == null)
                {
                    modOptionsMenuScreen = null;
                }

                isShowingModMenu = false;

                Logger.LogInfo("Mod menu state reset completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"ResetModMenuState error: {e}");
            }
        }

        private static void SafeCleanupModMenu()
        {
            try
            {
                Logger.LogInfo("Safe cleanup of mod menu...");

                // 安全销毁我们创建的对象
                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Destroy(modOptionsButton.gameObject);
                }
                modOptionsButton = null;

                if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject != null)
                {
                    Destroy(modOptionsMenuScreen.gameObject);
                }
                modOptionsMenuScreen = null;

                isShowingModMenu = false;
                modsContentParent = null;
                rowPrototype = null;

                Logger.LogInfo("Safe cleanup completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"SafeCleanupModMenu error: {e}");
            }
        }

        public static void CleanupModMenu()
        {
            try
            {
                Logger.LogInfo("Cleaning up mod menu references...");

                if (modOptionsButton != null && modOptionsButton.gameObject != null)
                {
                    Destroy(modOptionsButton.gameObject);
                }
                modOptionsButton = null;

                if (modOptionsMenuScreen != null && modOptionsMenuScreen.gameObject != null)
                {
                    Destroy(modOptionsMenuScreen.gameObject);
                }
                modOptionsMenuScreen = null;

                Logger.LogInfo("Mod menu cleanup completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"CleanupModMenu error: {e}");
            }
        }
    }
}
