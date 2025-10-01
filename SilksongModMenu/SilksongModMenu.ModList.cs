using BepInEx;
using BepInEx.Bootstrap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
        private static void BuildModsList()
        {
            Logger.LogInfo("=== BuildModsList START ===");
            Logger.LogInfo($"modOptionsMenuScreen: {modOptionsMenuScreen != null}");
            Logger.LogInfo($"modsContentParent: {modsContentParent != null}");
            Logger.LogInfo($"rowPrototype: {rowPrototype != null}");

            if (modOptionsMenuScreen == null)
            {
                Logger.LogError("modOptionsMenuScreen is null!");
                return;
            }

            if (modsContentParent == null || rowPrototype == null)
            {
                Logger.LogWarning("BuildModsList: content or rowPrototype missing, trying SetupModsContent again.");
                SetupModsContent(modOptionsMenuScreen.gameObject);

                Logger.LogInfo($"After retry - modsContentParent: {modsContentParent != null}");
                Logger.LogInfo($"After retry - rowPrototype: {rowPrototype != null}");

                if (modsContentParent == null || rowPrototype == null)
                {
                    Logger.LogError("BuildModsList failed: content parent or row prototype is null after retry.");
                    return;
                }
            }

            Logger.LogInfo($"Content parent children before clear: {modsContentParent.childCount}");

            // 清除旧行
            for (int i = modsContentParent.childCount - 1; i >= 0; i--)
            {
                var child = modsContentParent.GetChild(i).gameObject;
                if (child == rowPrototype)
                {
                    Logger.LogInfo($"Skipping prototype: {child.name}");
                    continue;
                }
                Logger.LogInfo($"Destroying old row: {child.name}");
                Destroy(child);
            }

            var mods = DiscoverLoadedPluginsForList();
            Logger.LogInfo($"Found {mods.Count} mods");

            if (mods.Count == 0)
            {
                CreateHintRow("No mods detected.");
            }
            else
            {
                foreach (var m in mods)
                {
                    Logger.LogInfo($"Creating row for mod: {m.displayName}");
                    CreateModRow_UseAchievementsStyle(m.displayName, m.enabled, m.onToggle);
                }
            }

            var rt = modsContentParent as RectTransform;
            if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Logger.LogInfo($"Content parent children after build: {modsContentParent.childCount}");
            Logger.LogInfo("=== BuildModsList END ===");
        }

        private static void CreateModRow_UseAchievementsStyle(string modName, bool initialEnabled, System.Action<bool> onToggle)
        {
            if (rowPrototype == null || modsContentParent == null)
            {
                Logger.LogError("Cannot create mod row: prototype or parent is null");
                return;
            }

            var newRow = Instantiate(rowPrototype, modsContentParent);
            newRow.name = "Row_" + modName;
            newRow.SetActive(true);

            Logger.LogInfo($"Creating mod row for: {modName}");

            bool currentState = initialEnabled;

            // 保存原始的 Selectable 配置
            var originalMenuOption = newRow.GetComponentInChildren<MenuOptionHorizontal>(true);
            Navigation originalNav = default;
            bool wasInteractable = true;

            if (originalMenuOption != null)
            {
                originalNav = originalMenuOption.navigation;
                wasInteractable = originalMenuOption.interactable;
                Logger.LogInfo($"Saved original navigation: {originalNav.mode}");
                DestroyImmediate(originalMenuOption);
            }

            // 处理文本
            var allTexts = newRow.GetComponentsInChildren<Text>(true);
            Text labelText = null;
            Text valueText = null;

            if (allTexts.Length > 0) labelText = allTexts[0];
            if (allTexts.Length > 1) valueText = allTexts[1];

            Logger.LogInfo($"Found {allTexts.Length} Text components");

            // 移除本地化
            foreach (var text in allTexts)
            {
                if (text == null) continue;

                var platformLocal = text.GetComponent<PlatformSpecificLocalisation>();
                if (platformLocal != null) DestroyImmediate(platformLocal);

                var autoLocal = text.GetComponent<AutoLocalizeTextUI>();
                if (autoLocal != null) DestroyImmediate(autoLocal);
            }

            // 更新显示
            void UpdateDisplay()
            {
                if (labelText != null) labelText.text = modName;
                if (valueText != null) valueText.text = currentState ? "ON" : "OFF";
            }

            UpdateDisplay();

            // 首次点击时动态调整位置
            void AdjustTextPositionOnFirstInteraction()
            {
                if (rowAdjustmentStatus.ContainsKey(modName) && rowAdjustmentStatus[modName])
                {
                    Logger.LogInfo($"[{modName}] Already adjusted in this session");
                    return;
                }

                Logger.LogInfo($"[{modName}] First click detected, adjusting positions...");

                if (labelText != null)
                {
                    var labelRT = labelText.GetComponent<RectTransform>();
                    if (labelRT != null)
                    {
                        Logger.LogInfo($"[{modName}] Label before: {labelRT.anchoredPosition}");
                        float offsetY = -9f;
                        labelRT.anchoredPosition = new Vector2(
                            labelRT.anchoredPosition.x,
                            labelRT.anchoredPosition.y + offsetY
                        );
                        Logger.LogInfo($"[{modName}] Label after: {labelRT.anchoredPosition}");
                    }
                }

                if (valueText != null)
                {
                    var valueRT = valueText.GetComponent<RectTransform>();
                    if (valueRT != null)
                    {
                        Logger.LogInfo($"[{modName}] Value before: {valueRT.anchoredPosition}");
                        float offsetY = -10f;
                        valueRT.anchoredPosition = new Vector2(
                            valueRT.anchoredPosition.x,
                            valueRT.anchoredPosition.y + offsetY
                        );
                        Logger.LogInfo($"[{modName}] Value after: {valueRT.anchoredPosition}");
                    }
                }

                rowAdjustmentStatus[modName] = true;
                Logger.LogInfo($"[{modName}] ✅ Position adjustment completed");
            }

            // 找到可交互对象
            GameObject interactableObj = null;
            Animator[] cursors = null;

            foreach (Transform child in newRow.transform)
            {
                if (child.name.Contains("Option"))
                {
                    interactableObj = child.gameObject;
                    cursors = child.GetComponentsInChildren<Animator>(true);
                    Logger.LogInfo($"Found option object: {child.name}, cursors: {cursors.Length}");
                    break;
                }
            }

            if (interactableObj != null)
            {
                var selectable = interactableObj.AddComponent<Selectable>();
                selectable.transition = Selectable.Transition.None;
                selectable.navigation = originalNav;
                selectable.interactable = wasInteractable;
                Logger.LogInfo("Rebuilt Selectable with original config");

                var eventTrigger = interactableObj.AddComponent<EventTrigger>();

                // 点击事件
                var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                pointerClick.callback.AddListener((data) =>
                {
                    AdjustTextPositionOnFirstInteraction();
                    currentState = !currentState;
                    UpdateDisplay();
                    onToggle?.Invoke(currentState);
                    Logger.LogInfo($"Toggled {modName} to {currentState}");
                });
                eventTrigger.triggers.Add(pointerClick);

                var submit = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
                submit.callback.AddListener((data) =>
                {
                    AdjustTextPositionOnFirstInteraction();
                    currentState = !currentState;
                    UpdateDisplay();
                    onToggle?.Invoke(currentState);
                    Logger.LogInfo($"Toggled {modName} to {currentState} (Submit)");
                });
                eventTrigger.triggers.Add(submit);

                // Hover/Select 事件
                var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                pointerEnter.callback.AddListener((data) =>
                {
                    Logger.LogInfo($"PointerEnter on {modName}");
                    selectable.OnPointerEnter(null);

                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("show");
                                cursor.SetBool("selected", true);
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(pointerEnter);

                var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                pointerExit.callback.AddListener((data) =>
                {
                    Logger.LogInfo($"PointerExit on {modName}");
                    selectable.OnPointerExit(null);

                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("hide");
                                cursor.SetBool("selected", false);
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(pointerExit);

                var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                select.callback.AddListener((data) =>
                {
                    Logger.LogInfo($"Select on {modName}");
                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("show");
                                cursor.SetBool("selected", true);
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(select);

                var deselect = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
                deselect.callback.AddListener((data) =>
                {
                    Logger.LogInfo($"Deselect on {modName}");
                    if (cursors != null)
                    {
                        foreach (var cursor in cursors)
                        {
                            if (cursor != null && cursor.gameObject.name.Contains("Cursor"))
                            {
                                cursor.SetTrigger("hide");
                                cursor.SetBool("selected", false);
                            }
                        }
                    }
                });
                eventTrigger.triggers.Add(deselect);

                Logger.LogInfo($"Configured EventTrigger for {modName}");
            }
            else
            {
                Logger.LogError($"Cannot find interactable object for {modName}");
            }
        }

        private static GameObject FindRowPrototype(Transform root)
        {
            var gmo = root.GetComponentInChildren<GameMenuOptions>(true);
            if (gmo != null && gmo.languageOption != null)
            {
                var p = gmo.languageOption.transform.parent;
                if (p != null) return p.gameObject;
            }

            var anySel = root.GetComponentsInChildren<MenuSelectable>(true).FirstOrDefault();
            if (anySel != null)
            {
                var p = anySel.transform.parent;
                return p ? p.gameObject : anySel.gameObject;
            }

            return null;
        }

        private static void CreateHintRow(string hint)
        {
            var baseText = modOptionsMenuScreen.GetComponentsInChildren<Text>(true).FirstOrDefault();
            var go = new GameObject("HintRow", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(modsContentParent, false);
            var txt = go.GetComponent<Text>();
            txt.text = hint;
            txt.fontSize = baseText ? Mathf.Max(18, baseText.fontSize - 6) : 20;
            txt.color = new Color(1, 1, 1, 0.6f);
            if (baseText && baseText.font) txt.font = baseText.font;
        }

        private struct PluginItem
        {
            public string displayName;
            public bool enabled;
            public Action<bool> onToggle;
        }

        // 使用元数据管理系统
        private static List<PluginItem> DiscoverLoadedPluginsForList()
        {
            var list = new List<PluginItem>();
            var processedDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                ModMetadataManager.Load();

                // 1️⃣ 扫描已加载的 Mod
                foreach (var kv in Chainloader.PluginInfos)
                {
                    var pi = kv.Value;
                    if (pi?.Metadata == null) continue;

                    string guid = pi.Metadata.GUID;
                    if (guid == "com.yourname.silksongmodmenu") continue;

                    string name = string.IsNullOrEmpty(pi.Metadata.Name) ? guid : pi.Metadata.Name;
                    string version = pi.Metadata.Version?.ToString() ?? "Unknown";
                    string author = "Unknown";
                    string dllFileName = Path.GetFileName(pi.Location);

                    // 从元数据获取状态（通过 DLL 文件名）
                    var meta = ModMetadataManager.GetModByDll(dllFileName);
                    bool enabled = meta?.Enabled ?? true;

                    // 更新元数据（补充 GUID 信息）
                    ModMetadataManager.UpdateMod(dllFileName, guid, name, version, author, enabled);

                    list.Add(new PluginItem
                    {
                        displayName = name,
                        enabled = enabled,
                        onToggle = v =>
                        {
                            ModMetadataManager.SetEnabledByDll(dllFileName, v);
                            Logger.LogInfo($"[Mod Toggle] {name} ({dllFileName}) -> {(v ? "On" : "Off")}");
                        }
                    });

                    processedDlls.Add(dllFileName);
                    Logger.LogInfo($"[Loaded Mod] {name} ({dllFileName})");
                }

                // 2️⃣ 扫描 .disabled 文件
                string pluginsDir = Paths.PluginPath;
                if (Directory.Exists(pluginsDir))
                {
                    var disabledFiles = Directory.GetFiles(pluginsDir, "*.dll.disabled", SearchOption.TopDirectoryOnly);

                    foreach (var disabledPath in disabledFiles)
                    {
                        try
                        {
                            string fullFileName = Path.GetFileName(disabledPath); // ChairTeleport.dll.disabled
                            string dllFileName = Path.GetFileNameWithoutExtension(fullFileName); // ChairTeleport.dll

                            if (processedDlls.Contains(dllFileName))
                            {
                                Logger.LogInfo($"[Disabled Mod] {dllFileName} already processed, skipping");
                                continue;
                            }

                            // 从元数据获取信息
                            var meta = ModMetadataManager.GetModByDll(dllFileName);
                            string displayName = meta?.Name ?? Path.GetFileNameWithoutExtension(dllFileName);
                            bool enabled = meta?.Enabled ?? false;

                            // 如果元数据不存在，创建新记录
                            if (meta == null)
                            {
                                ModMetadataManager.UpdateMod(dllFileName, "", displayName, "Unknown", "Unknown", false);
                            }

                            list.Add(new PluginItem
                            {
                                displayName = displayName,
                                enabled = enabled,
                                onToggle = v =>
                                {
                                    ModMetadataManager.SetEnabledByDll(dllFileName, v);
                                    Logger.LogInfo($"[Mod Toggle] {displayName} ({dllFileName}) -> {(v ? "On" : "Off")}");
                                }
                            });

                            processedDlls.Add(dllFileName);
                            Logger.LogInfo($"[Disabled Mod] {displayName} ({dllFileName})");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Failed to process disabled mod {disabledPath}: {ex.Message}");
                        }
                    }
                }

                ModMetadataManager.Save();
            }
            catch (Exception e)
            {
                Logger.LogError($"DiscoverLoadedPluginsForList error: {e}");
            }

            return list.OrderBy(p => p.displayName).ToList();
        }

    }
}
