using MSCLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BackupSave
{
    public class SaveManager : Mod
    {
        private const string LOG_PREFIX = "[BackupSave] ";
        private const string NO_BACKUPS_COLOR = "#ffaa00";
        private const float MENU_LOCALIZATION_REAPPLY_DELAY = 0.02f;
        public override string ID => "BackupSave";
        public override string Name => "BackupSave";
        public override string Author => "LucasMonOficial";
        public override string Version => "2.0.2";
        public override string Description => LocalizationManager.Text(
            "Advanced automatic backup system with easy restore and backup limit control.",
            "Sistema avançado de backup automático com restauração fácil, controle de limite de backups.");
        public override Game SupportedGames => Game.MySummerCar | Game.MyWinterCar;

        private BackupManager backupManager;
        private SaveImportManager saveImportManager;
        private AutoRestoreManager autoRestoreManager;
        private LocalizationManager localizationManager;
        readonly string MSCSaves = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow") + "/Amistech";

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.Update, ModUpdate);
            SetupFunction(Setup.OnMenuLoad, OnMenuLoad);
            
            string gameFolder = GetGameSaveFolder();
            localizationManager = new LocalizationManager(gameFolder);
            backupManager = new BackupManager(MSCSaves, localizationManager);
            saveImportManager = new SaveImportManager(MSCSaves, backupManager, localizationManager);
            autoRestoreManager = new AutoRestoreManager(backupManager, MSCSaves, localizationManager);
            LoadUiSettings();
        }

        private int backupLimitValue = 50;
        private int autoRestoreModeValue = 1;
        private bool autoDeleteMeshsaveValue = false;
        private bool prefixCharacterNameValue = false;
        
        // Listas de backups/restore points carregadas no Mod_OnLoad (consolidadas)
        private Dictionary<string, string[]> backupLists = new Dictionary<string, string[]>();
        private bool listsLoaded = false;
        private bool autoMeshsaveDeleteAttemptedInMenu = false;
        private bool menuLocalizationReapplyPending = false;
        private float menuLocalizationReapplyAt = 0f;
        private MenuBackupPanel menuBackupPanel;
        
        private string GetGameSaveFolder()
            => ModLoader.CurrentGame == Game.MySummerCar ? "My Summer Car" : "My Winter Car";


        private int GetBackupLimit()
            => backupLimitValue;

        private int GetAutoRestoreMode()
            => autoRestoreModeValue;

        private bool GetAutoDeleteMeshsave()
            => autoDeleteMeshsaveValue;

        private bool GetPrefixCharacterName()
            => prefixCharacterNameValue;

        private void SetBackupLimit(int value)
        {
            backupLimitValue = Mathf.Clamp(value, 0, 100);
            SaveUiSettings();
        }

        private void SetAutoRestoreMode(int value)
        {
            autoRestoreModeValue = Mathf.Clamp(value, 0, 2);
            SaveUiSettings();
        }

        private void SetAutoDeleteMeshsave(bool value)
        {
            autoDeleteMeshsaveValue = value;
            SaveUiSettings();
        }

        private void SetPrefixCharacterName(bool value)
        {
            prefixCharacterNameValue = value;
            SaveUiSettings();
        }

        private string GetSettingsKey(string settingName)
        {
            return "BackupSave_" + GetGameSaveFolder().Replace(" ", "") + "_" + settingName;
        }

        private void LoadUiSettings()
        {
            autoRestoreModeValue = Mathf.Clamp(PlayerPrefs.GetInt(GetSettingsKey("AutoRestoreMode"), 1), 0, 2);
            backupLimitValue = Mathf.Clamp(PlayerPrefs.GetInt(GetSettingsKey("BackupLimit"), 50), 0, 100);
            autoDeleteMeshsaveValue = PlayerPrefs.GetInt(GetSettingsKey("AutoDeleteMeshsave"), 0) == 1;
            prefixCharacterNameValue = PlayerPrefs.GetInt(GetSettingsKey("PrefixCharacterName"), 0) == 1;
        }

        private void SaveUiSettings()
        {
            PlayerPrefs.SetInt(GetSettingsKey("AutoRestoreMode"), autoRestoreModeValue);
            PlayerPrefs.SetInt(GetSettingsKey("BackupLimit"), backupLimitValue);
            PlayerPrefs.SetInt(GetSettingsKey("AutoDeleteMeshsave"), autoDeleteMeshsaveValue ? 1 : 0);
            PlayerPrefs.SetInt(GetSettingsKey("PrefixCharacterName"), prefixCharacterNameValue ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void EnsureListsLoaded()
        {
            if (listsLoaded) return;
            if (backupManager == null) return;
            
            backupLists["mscBackup"] = backupManager.GetBackupList("My Summer Car");
            backupLists["mwcBackup"] = backupManager.GetBackupList("My Winter Car");
            backupLists["mscRestorePoint"] = backupManager.GetRestorePointList("My Summer Car");
            backupLists["mwcRestorePoint"] = backupManager.GetRestorePointList("My Winter Car");
            listsLoaded = true;
        }

        private void RefreshBackupLists()
        {
            listsLoaded = false;
            EnsureListsLoaded();
        }

        private string[] GetSavesDropdownItems(string gameFolder)
        {
            string[] unifiedList = GetUnifiedBackupAndRestorePointList(gameFolder);
            return unifiedList.Length == 0 ? new string[] { GetNoBackupsItem() } : unifiedList;
        }

        private bool IsNoBackupsItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return true;

            string cleaned = RemoveListColorTags(itemName);
            return cleaned == LocalizationManager.Text("No backups available", "Nenhum backup disponível")
                || cleaned == "No backups available"
                || cleaned == "Nenhum backup disponivel"
                || cleaned == "Nenhum backup disponível";
        }

        private string GetNoBackupsItem()
        {
            string text = LocalizationManager.Text("No backups available", "Nenhum backup disponível");
            if (localizationManager != null)
            {
                text = localizationManager.GetString("label", "noBackupsItem", text);
            }
            return "<color=" + NO_BACKUPS_COLOR + ">" + text + "</color>";
        }

        private string[] GetUnifiedBackupAndRestorePointList(string gameFolder)
        {
            string backupKey = gameFolder == "My Summer Car" ? "mscBackup" : "mwcBackup";
            string rpKey = gameFolder == "My Summer Car" ? "mscRestorePoint" : "mwcRestorePoint";
            
            string[] backupList = backupLists.ContainsKey(backupKey) ? backupLists[backupKey] : new string[] { };
            string[] restorePointList = backupLists.ContainsKey(rpKey) ? backupLists[rpKey] : new string[] { };
            
            int totalCount = backupList.Length + restorePointList.Length;
            string[] unifiedList = new string[totalCount];
            
            for (int i = 0; i < backupList.Length; i++)
                unifiedList[i] = backupList[i].StartsWith("<color=#21ff13>") ? backupList[i] : "<color=#21ff13>" + backupList[i] + "</color>";
            
            for (int i = 0; i < restorePointList.Length; i++)
            {
                string rpName = restorePointList[i];
                unifiedList[backupList.Length + i] = rpName.StartsWith("<color=#ff9900>") ? rpName : "<color=#ff9900>" + GetRestorePointListPrefix() + rpName + "</color>";
            }
            
            return unifiedList;
        }

        private string GetRestorePointListPrefix()
        {
            return LocalizationManager.Text("RP: ", "PR: ");
        }

        private string GetMenuPanelTitle()
        {
            return localizationManager.GetString("header", "menuBackupManager", LocalizationManager.Text("BACKUP MANAGER", "GERENCIADOR DE BACKUPS"));
        }

        private string GetManageBackupsButtonText()
        {
            return localizationManager.GetString("button", "manageBackups", "BACKUPS");
        }

        private string[] GetMenuPanelBackupItems()
        {
            EnsureListsLoaded();
            return GetSavesDropdownItems(GetGameSaveFolder());
        }

        // Verifica se um item da lista unificada é um ponto de restauração
        private bool IsRestorePointItem(string itemName)
        {
            return itemName.StartsWith("<color=#ff9900>");
        }

        // Remove as tags de cor e prefixo de ponto de restauração se existir
        private string GetCleanBackupName(string itemName)
        {
            string cleaned = RemoveListColorTags(itemName);
            string restorePointPrefix = GetRestorePointListPrefix();
            if (cleaned.StartsWith(restorePointPrefix))
                cleaned = cleaned.Substring(restorePointPrefix.Length);
            else if (cleaned.StartsWith("PR: "))
                cleaned = cleaned.Substring("PR: ".Length);
            else if (cleaned.StartsWith("RP: "))
                cleaned = cleaned.Substring("RP: ".Length);
            return cleaned;
        }

        private string RemoveListColorTags(string itemName)
        {
            return itemName.Replace("<color=#ff9900>", "").Replace("<color=#21ff13>", "").Replace("<color=" + NO_BACKUPS_COLOR + ">", "").Replace("</color>", "");
        }

        private void RestoreSelectedBackupItem(string selectedName)
        {
            if (IsNoBackupsItem(selectedName))
                return;

            string cleanName = GetCleanBackupName(selectedName);
            bool isRestorePoint = IsRestorePointItem(selectedName);
            bool success = isRestorePoint ?
                backupManager.RestoreRestorePointByName(GetGameSaveFolder(), cleanName) :
                backupManager.RestoreBackupByName(GetGameSaveFolder(), cleanName);
            if (success)
                RefreshBackupLists();
        }

        private void DeleteSelectedBackupItem(string selectedName)
        {
            if (IsNoBackupsItem(selectedName))
                return;

            string gameFolder = GetGameSaveFolder();
            string cleanName = GetCleanBackupName(selectedName);
            bool isRestorePoint = IsRestorePointItem(selectedName);
            bool success = isRestorePoint ?
                backupManager.DeleteRestorePointByName(gameFolder, cleanName) :
                backupManager.DeleteBackupByName(gameFolder, cleanName);
            if (success)
                RefreshBackupLists();
        }

        private void CreateRestorePointFromName(string restorePointName)
        {
            string gameFolder = GetGameSaveFolder();
            string createdPointName = backupManager.CreateRestorePoint(gameFolder, restorePointName);
            if (!string.IsNullOrEmpty(createdPointName))
                RefreshBackupLists();
        }
        private void OnDeleteMeshSaveClick()
        {
            backupManager.DeleteMeshSaveFile(GetGameSaveFolder());
        }

        private void OnRestartLevel1Click()
        {
            ScheduleMenuLocalizationReapply();
            UnityEngine.Application.LoadLevel(1);
        }

        private void ScheduleMenuLocalizationReapply()
        {
            if (!LocalizationManager.IsBrazilianLocalizationInstalled())
                return;

            menuLocalizationReapplyPending = true;
            menuLocalizationReapplyAt = UnityEngine.Time.realtimeSinceStartup + MENU_LOCALIZATION_REAPPLY_DELAY;
        }

        private void ProcessMenuLocalizationReapply()
        {
            if (!menuLocalizationReapplyPending)
                return;
            if (UnityEngine.Application.loadedLevel != 1)
                return;
            if (UnityEngine.Time.realtimeSinceStartup < menuLocalizationReapplyAt)
                return;

            menuLocalizationReapplyPending = false;
            if (TryReapplyExternalLocalization())
                return;
        }

        private bool TryReapplyExternalLocalization()
        {
            try
            {
                Mod localizationMod = GetLoadedModById(LocalizationManager.GetBrazilianLocalizationModId());
                if (localizationMod == null)
                    return false;

                bool reapplied = false;
                if (TryInvokeModCallback(localizationMod, "A_OnMenuLoad"))
                    reapplied = true;
                else if (TryInvokeNoArgsMethod(localizationMod, "Mod_OnMenuLoad"))
                    reapplied = true;

                if (TryInvokeNoArgsMethod(localizationMod, "ReloadTranslations"))
                    reapplied = true;

                return reapplied;
            }
            catch (Exception)
            {
                menuLocalizationReapplyPending = false;
                return true;
            }
        }

        private Mod GetLoadedModById(string modId)
        {
            if (string.IsNullOrEmpty(modId) || ModLoader.LoadedMods == null)
                return null;

            foreach (Mod mod in ModLoader.LoadedMods)
            {
                if (mod != null && string.Equals(mod.ID, modId, StringComparison.OrdinalIgnoreCase))
                    return mod;
            }

            return null;
        }

        private bool TryInvokeModCallback(Mod mod, string callbackFieldName)
        {
            FieldInfo callbackField = typeof(Mod).GetField(callbackFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Action callback = callbackField != null ? callbackField.GetValue(mod) as Action : null;
            if (callback == null)
                return false;

            callback();
            return true;
        }

        private bool TryInvokeNoArgsMethod(Mod mod, string methodName)
        {
            MethodInfo method = mod.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
                return false;

            method.Invoke(mod, null);
            return true;
        }

        private void OnImportSaveClick()
        {
            string customBackupName = "MWC-Backup";
            int backupLimit = GetBackupLimit();
            
            saveImportManager.ImportSaveFromMSCToMWC(backupManager, customBackupName, backupLimit);
            RefreshBackupLists();
        }



        private void OnImportAllExternalBackupsClick()
        {
            int backupLimit = GetBackupLimit();
            if (saveImportManager.ImportAllExternalBackups(backupLimit) > 0)
                RefreshBackupLists();
        }

        private bool CanShowImportSaveFromMSC()
        {
            return ModLoader.CurrentGame == Game.MyWinterCar && saveImportManager != null && saveImportManager.HasMSCSave();
        }

        private bool CanShowExternalBackupImport()
        {
            return saveImportManager != null && saveImportManager.HasExternalBackups();
        }

        private void OnOpenBackupFolderClick()
        {
            string gameFolder = GetGameSaveFolder();
            string backupFolderPath = backupManager.GetBackupRootPath(gameFolder);
            
            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = backupFolderPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        private void OnOpenSaveFolderClick()
        {
            string saveFolderPath = Path.Combine(MSCSaves, GetGameSaveFolder());
            if (!Directory.Exists(saveFolderPath))
                Directory.CreateDirectory(saveFolderPath);

            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = saveFolderPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        private void EnsureMenuBackupPanel()
        {
            if (menuBackupPanel != null)
                return;

            GameObject panelObject = new GameObject("BackupSaveMenuPanel");
            menuBackupPanel = panelObject.AddComponent<MenuBackupPanel>();
            menuBackupPanel.Initialize(this);
            UnityEngine.Object.DontDestroyOnLoad(panelObject);
        }

        private class MenuBackupPanel : MonoBehaviour
        {
            private SaveManager owner;
            private bool isVisible;
            private bool showImportSave;
            private bool showExternalImport;
            private int selectedIndex;
            private string restorePointName = "";
            private Vector2 listScroll = Vector2.zero;
            private Vector2 detailScroll = Vector2.zero;
            private string detailTitle = "";
            private string detailText = "";
            private GameObject menuButton;
            private Texture2D overlayTexture;
            private Texture2D selectedTexture;
            private Texture2D listTexture;
            private Texture2D sectionTexture;
            private Texture2D whiteTexture;
            private Texture2D rowTexture;
            private Texture2D rowHoverTexture;
            private Texture2D actionTexture;
            private Texture2D scrollbarTrackTexture;
            private Texture2D scrollbarThumbTexture;
            private Color themeColor;
            private Color themeDarkColor;
            private GUIStyle titleStyle;
            private GUIStyle labelStyle;
            private GUIStyle noteStyle;
            private GUIStyle sectionHeaderStyle;
            private GUIStyle buttonStyle;
            private GUIStyle deleteButtonStyle;
            private GUIStyle itemStyle;
            private GUIStyle selectedItemStyle;
            private GUIStyle fieldStyle;
            private GUIStyle listBoxStyle;
            private GUIStyle stepperButtonStyle;
            private GUIStyle stepperValueStyle;
            private GUIStyle detailTextStyle;
            private GUIStyle titleShadowStyle;
            private GUIStyle placeholderFieldStyle;

            public void Initialize(SaveManager modOwner)
            {
                owner = modOwner;
                RefreshData();
                EnsureMenuButton();
            }

            public void Hide()
            {
                isVisible = false;
            }

            private void Toggle()
            {
                isVisible = !isVisible;
                if (isVisible)
                    RefreshData();
            }

            private void RefreshData()
            {
                if (owner == null)
                    return;

                owner.RefreshBackupLists();
                RefreshImportVisibility();
                ClampSelectedIndex();
                UpdateMenuButtonText();
            }

            private void RefreshImportVisibility()
            {
                showImportSave = owner.CanShowImportSaveFromMSC();
                showExternalImport = owner.CanShowExternalBackupImport();
            }

            private void OnGUI()
            {
                if (owner == null || owner.localizationManager == null || owner.backupManager == null)
                    return;

                if (UnityEngine.Application.loadedLevel != 1)
                {
                    isVisible = false;
                    menuButton = null;
                    return;
                }

                EnsureMenuButton();
                InitStyles();

                if (isVisible)
                {
                    if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        isVisible = false;
                        Event.current.Use();
                        return;
                    }

                    DrawPanel();
                }
            }

            private void EnsureMenuButton()
            {
                if (owner == null)
                    return;

                Transform buttonsRoot = FindButtonsRoot();

                if (menuButton != null)
                {
                    PositionMenuButton(buttonsRoot, menuButton);
                    UpdateMenuButtonText();
                    return;
                }

                Transform existing = FindMenuTransform(buttonsRoot, "ButtonManageBackups");
                if (existing != null)
                {
                    menuButton = existing.gameObject;
                    PositionMenuButton(buttonsRoot, menuButton);
                    FinalizeMenuButton(menuButton);
                    return;
                }

                Transform reference = GetMenuButtonReference(buttonsRoot);
                if (reference == null)
                    return;

                menuButton = UnityEngine.Object.Instantiate(reference.gameObject) as GameObject;
                if (menuButton == null)
                    return;

                menuButton.name = "ButtonManageBackups";
                menuButton.transform.SetParent(buttonsRoot != null ? buttonsRoot : reference.parent, false);
                menuButton.transform.localScale = reference.localScale;
                menuButton.transform.localRotation = reference.localRotation;
                PositionMenuButton(buttonsRoot, menuButton);
                FinalizeMenuButton(menuButton);
            }

            private Transform GetMenuButtonReference(Transform buttonsRoot)
            {
                Transform continueButton = FindMenuTransform(buttonsRoot, "ButtonContinue");
                if (CanUseContinueButton(continueButton))
                    return continueButton;

                Transform newButton = FindMenuTransform(buttonsRoot, "ButtonNew", "ButtonNewGame");
                if (newButton != null)
                    return newButton;

                Transform newButtonByText = FindMenuTransformByText(buttonsRoot, "NEW GAME", "NOVO JOGO");
                if (newButtonByText != null)
                    return newButtonByText;

                Transform creditsButton = FindMenuTransform(buttonsRoot, "ButtonCredits", "ButtonQuit");
                if (creditsButton != null)
                    return creditsButton;

                return FindMenuTransformByText(buttonsRoot, "CREDITS", "CRÉDITOS", "CREDITOS", "QUIT", "SAIR");
            }

            private void PositionMenuButton(Transform buttonsRoot, GameObject button)
            {
                Transform continueButton = FindMenuTransform(buttonsRoot, "ButtonContinue");
                Transform newButton = FindMenuTransform(buttonsRoot, "ButtonNew", "ButtonNewGame");
                bool useContinue = CanUseContinueButton(continueButton);
                Transform reference = useContinue ? continueButton : newButton;
                if (reference == null)
                    reference = useContinue ? null : FindMenuTransformByText(buttonsRoot, "NEW GAME", "NOVO JOGO");
                if (reference == null || button == null)
                    return;

                if (useContinue)
                {
                    Vector3 continuePosition = reference.localPosition;
                    button.transform.localPosition = new Vector3(continuePosition.x + 0.018f, continuePosition.y + 0.095f, continuePosition.z);
                    return;
                }

                Transform creditsButton = FindMenuTransform(buttonsRoot, "ButtonCredits");
                if (creditsButton == null)
                    creditsButton = FindMenuTransformByText(buttonsRoot, "CREDITS", "CRÉDITOS", "CREDITOS");

                Vector3 noSaveOffset = new Vector3(0f, 0.105f, 0f);
                if (creditsButton != null && creditsButton.parent == reference.parent)
                {
                    Vector3 menuStep = reference.localPosition - creditsButton.localPosition;
                    if (menuStep.sqrMagnitude > 0.0001f)
                        noSaveOffset = menuStep * 0.92f;
                }

                button.transform.localPosition = reference.localPosition + noSaveOffset;
            }

            private Transform FindButtonsRoot()
            {
                GameObject root = GameObject.Find("Interface/Buttons");
                if (root != null)
                    return root.transform;

                Transform menuButtonReference = FindMenuTransform(null, "ButtonContinue", "ButtonNew", "ButtonNewGame", "ButtonCredits", "ButtonQuit");
                if (menuButtonReference != null && menuButtonReference.parent != null)
                    return menuButtonReference.parent;

                menuButtonReference = FindMenuTransformByText(null, "CONTINUE", "CONTINUAR", "NEW GAME", "NOVO JOGO", "CREDITS", "CRÉDITOS", "CREDITOS", "QUIT", "SAIR");
                return menuButtonReference != null ? menuButtonReference.parent : null;
            }

            private Transform FindMenuTransform(Transform root, params string[] names)
            {
                if (names == null || names.Length == 0)
                    return null;

                if (root != null)
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        Transform directChild = root.Find(names[i]);
                        if (directChild != null)
                            return directChild;
                    }
                }

                Transform[] transforms = UnityEngine.Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null || !IsInUsableScene(candidate.gameObject))
                        continue;
                    if (root != null && !candidate.IsChildOf(root))
                        continue;

                    for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
                    {
                        if (string.Equals(candidate.name, names[nameIndex], StringComparison.OrdinalIgnoreCase))
                            return candidate;
                    }
                }

                return null;
            }

            private Transform FindMenuTransformByText(Transform root, params string[] texts)
            {
                if (texts == null || texts.Length == 0)
                    return null;

                TextMesh[] textMeshes = UnityEngine.Resources.FindObjectsOfTypeAll<TextMesh>();
                for (int i = 0; i < textMeshes.Length; i++)
                {
                    TextMesh textMesh = textMeshes[i];
                    if (textMesh == null || string.IsNullOrEmpty(textMesh.text) || !IsInUsableScene(textMesh.gameObject))
                        continue;

                    Transform candidate = textMesh.transform;
                    if (root != null && !candidate.IsChildOf(root))
                        continue;

                    string buttonText = textMesh.text.ToUpperInvariant();
                    for (int textIndex = 0; textIndex < texts.Length; textIndex++)
                    {
                        if (buttonText.IndexOf(texts[textIndex].ToUpperInvariant()) >= 0)
                            return GetButtonRoot(candidate, root);
                    }
                }

                return null;
            }

            private Transform GetButtonRoot(Transform candidate, Transform buttonsRoot)
            {
                if (candidate == null || buttonsRoot == null)
                    return candidate;

                Transform current = candidate;
                while (current.parent != null && current.parent != buttonsRoot)
                    current = current.parent;

                return current;
            }

            private bool IsInUsableScene(GameObject gameObject)
            {
                return gameObject != null && gameObject.hideFlags == HideFlags.None;
            }

            private bool CanUseContinueButton(Transform continueButton)
            {
                return IsMenuButtonVisible(continueButton)
                    && owner != null
                    && owner.backupManager != null
                    && owner.backupManager.ValidateSaveFiles(owner.GetGameSaveFolder());
            }

            private bool IsMenuButtonVisible(Transform button)
            {
                if (button == null || !button.gameObject.activeInHierarchy)
                    return false;

                Renderer[] renderers = button.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return true;

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy)
                        return true;
                }

                return false;
            }

            private void FinalizeMenuButton(GameObject button)
            {
                UpdateMenuButtonText(button);

                BoxCollider collider = button.GetComponent<BoxCollider>();
                if (collider == null)
                    collider = button.AddComponent<BoxCollider>();
                if (collider != null)
                {
                    Vector3 size = collider.size;
                    if (size == Vector3.zero)
                        size = new Vector3(2.8f, 0.45f, 0.35f);
                    else
                        size.x += 2f;
                    collider.size = size;
                }

                Component[] components = button.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component != null && component.GetType().Name == "PlayMakerFSM")
                        UnityEngine.Object.Destroy(component);
                }

                MenuButtonClickHandler handler = button.GetComponent<MenuButtonClickHandler>();
                if (handler == null)
                    handler = button.AddComponent<MenuButtonClickHandler>();
                handler.SetCallback(new Action(Toggle));
            }

            private void UpdateMenuButtonText()
            {
                if (menuButton != null)
                    UpdateMenuButtonText(menuButton);
            }

            private void UpdateMenuButtonText(GameObject button)
            {
                if (owner == null || button == null)
                    return;

                TextMesh[] textMeshes = button.GetComponentsInChildren<TextMesh>();
                for (int i = 0; i < textMeshes.Length; i++)
                    textMeshes[i].text = owner.GetManageBackupsButtonText();
            }

            private void DrawPanel()
            {
                RefreshImportVisibility();

                Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.38f);
                GUI.DrawTexture(screenRect, overlayTexture);
                GUI.color = oldColor;

                float width = Mathf.Min(1120f, Screen.width - 10f);
                float height = GetPanelHeight();
                float panelY = Mathf.Max(8f, (Screen.height - height) * 0.5f - 150f);
                Rect panelRect = new Rect((Screen.width - width) * 0.5f, panelY, width, height);

                if (Event.current != null && Event.current.type == EventType.MouseDown && !panelRect.Contains(Event.current.mousePosition))
                {
                    isVisible = false;
                    Event.current.Use();
                    return;
                }

                DrawBorderedPanel(panelRect, themeColor, 3f);

                DrawGameTitle(panelRect);

                Rect contentRect = new Rect(panelRect.x + 12f, panelRect.y + 62f, panelRect.width - 24f, panelRect.height - 72f);
                GUI.Box(contentRect, GUIContent.none, listBoxStyle);

                Rect innerContentRect = new Rect(contentRect.x + 6f, contentRect.y + 6f, contentRect.width - 12f, contentRect.height - 12f);
                float sideWidth = Mathf.Min(390f, Mathf.Max(350f, innerContentRect.width * 0.38f));
                Rect mainRect = new Rect(innerContentRect.x, innerContentRect.y, innerContentRect.width - sideWidth - 10f, innerContentRect.height);
                Rect sideRect = new Rect(mainRect.xMax + 10f, innerContentRect.y, sideWidth, innerContentRect.height);

                GUILayout.BeginArea(mainRect);
                DrawBackupSection(mainRect.width, mainRect.height);
                GUILayout.EndArea();

                GUILayout.BeginArea(sideRect);
                Rect sideTopRect = new Rect(0f, 0f, sideRect.width, Mathf.Max(0f, sideRect.height - 105f));
                GUILayout.BeginArea(sideTopRect);
                GUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawSaveSettingsSection();
                GUILayout.Space(4f);
                DrawMeshsaveSection();
                GUILayout.Space(4f);
                DrawImportSection();
                GUILayout.EndVertical();
                GUILayout.EndArea();
                DrawFooterActions(new Rect(0f, sideRect.height - 97f, sideRect.width, 97f));
                GUILayout.EndArea();

                DrawDetailOverlay(panelRect);
            }

            private float GetPanelHeight()
            {
                float desiredHeight = 630f;
                if (showImportSave)
                    desiredHeight += 56f;
                if (showExternalImport)
                    desiredHeight += 56f;

                return Mathf.Clamp(desiredHeight, 600f, Screen.height - 8f);
            }

            private void DrawGameTitle(Rect panelRect)
            {
                Rect titleRect = new Rect(panelRect.x + 12f, panelRect.y + 8f, panelRect.width - 24f, 50f);
                DrawBorderedPanel(titleRect, themeColor, 2f);
                GUI.Label(new Rect(titleRect.x + 3f, titleRect.y + 3f, titleRect.width, titleRect.height), owner.GetMenuPanelTitle(), titleShadowStyle);
                GUI.Label(titleRect, owner.GetMenuPanelTitle(), titleStyle);
            }

            private void DrawSaveSettingsSection()
            {
                DrawSectionHeader(owner.localizationManager.GetString("header", "settings", LocalizationManager.Text("SAVE SETTINGS", "CONFIGURAÇÕES DE SAVE")));
                GUILayout.Label(owner.localizationManager.GetString("label", "autoRestoreMode", LocalizationManager.Text("Automatic Restore Mode", "Modo de Restauração Automática")), labelStyle);

                int mode = owner.GetAutoRestoreMode();
                string[] modes = owner.localizationManager.GetAutoRestoreModeValues();
                int newMode = DrawStepper(mode, 0, modes.Length - 1, modes[Mathf.Clamp(mode, 0, modes.Length - 1)], true);
                if (newMode != mode)
                    owner.SetAutoRestoreMode(newMode);

                GUILayout.Space(8f);
                int limit = owner.GetBackupLimit();
                GUILayout.Label(owner.localizationManager.GetString("label", "backupLimit", LocalizationManager.Text("Backup Limit", "Limite de Backups")), labelStyle);
                int newLimit = DrawStepper(limit, 0, 100, limit == 0 ? LocalizationManager.Text("Unlimited", "Ilimitado") : limit.ToString(), false);
                if (newLimit != limit)
                    owner.SetBackupLimit(newLimit);

                GUILayout.Space(8f);
                bool prefixEnabled = owner.GetPrefixCharacterName();
                bool newPrefixEnabled = DrawLargeToggle(prefixEnabled, owner.localizationManager.GetString("label", "prefixCharacterName", LocalizationManager.Text("Prefix backup with character name", "Prefixar backups com o nome do personagem")));
                if (newPrefixEnabled != prefixEnabled)
                    owner.SetPrefixCharacterName(newPrefixEnabled);
            }

            private void DrawBackupSection(float availableWidth, float availableHeight)
            {
                const float footerHeight = 97f;
                const float footerGap = 8f;
                Rect topRect = new Rect(0f, 0f, availableWidth, Mathf.Max(0f, availableHeight - footerHeight - footerGap));
                Rect footerRect = new Rect(0f, availableHeight - footerHeight, availableWidth, footerHeight);

                GUILayout.BeginArea(topRect);
                GUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawSectionHeader(owner.localizationManager.GetString("header", "backupsAndPoints", LocalizationManager.Text("BACKUPS AND RESTORE POINTS", "BACKUPS E PONTOS DE RESTAURAÇÃO")));
                DrawBackupList(GetBackupListHeight(topRect.height));
                GUILayout.Space(6f);
                DrawRestorePointNameField();
                GUILayout.EndVertical();
                GUILayout.EndArea();

                DrawBackupActions(footerRect);
            }

            private float GetBackupListHeight(float availableHeight)
            {
                return Mathf.Max(120f, availableHeight - 76f);
            }

            private void DrawBackupList(float listHeight)
            {
                string[] items = owner.GetMenuPanelBackupItems();
                ClampSelectedIndex(items);

                GUILayout.BeginVertical(GUIStyle.none, GUILayout.Height(listHeight), GUILayout.ExpandWidth(true));
                listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.ExpandWidth(true), GUILayout.Height(listHeight));
                if (items.Length == 0 || owner.IsNoBackupsItem(items[0]))
                {
                    GUILayout.Label(owner.GetNoBackupsItem(), itemStyle, GUILayout.Height(30f), GUILayout.ExpandWidth(true));
                }
                else
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        GUIStyle style = i == selectedIndex ? selectedItemStyle : itemStyle;
                        if (GUILayout.Button(items[i], style, GUILayout.Height(30f), GUILayout.ExpandWidth(true)))
                            selectedIndex = i;
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            private void DrawBackupActions(Rect footerRect)
            {
                string selected = GetSelectedItem();
                bool hasSelection = !owner.IsNoBackupsItem(selected);

                Rect primaryRow = new Rect(footerRect.x, footerRect.y, footerRect.width, 54f);
                GUI.enabled = hasSelection;
                if (DrawColoredButtonInRow(primaryRow, 0, 3, owner.localizationManager.GetString("button", "restore", LocalizationManager.Text("RESTORE", "RESTAURAR")), new Color(0f, 0.55f, 0.24f, 1f)))
                    owner.RestoreSelectedBackupItem(selected);
                if (DrawColoredButtonInRow(primaryRow, 1, 3, owner.localizationManager.GetString("button", "delete", LocalizationManager.Text("DELETE", "DELETAR")), new Color(0.72f, 0.03f, 0.03f, 1f)))
                {
                    owner.DeleteSelectedBackupItem(selected);
                    ClampSelectedIndex();
                }
                GUI.enabled = true;
                if (DrawColoredButtonInRow(primaryRow, 2, 3, owner.localizationManager.GetString("button", "createRestorePoint", LocalizationManager.Text("CREATE POINT", "CRIAR PONTO")), new Color(0.85f, 0.62f, 0.02f, 1f)))
                {
                    owner.CreateRestorePointFromName(restorePointName);
                    restorePointName = "";
                    ClampSelectedIndex();
                }

                Rect secondaryRow = new Rect(footerRect.x, footerRect.y + 61f, footerRect.width, 36f);
                if (DrawColoredButtonInRow(secondaryRow, 0, 3, owner.localizationManager.GetString("button", "restartMenu", LocalizationManager.Text("RESTART MENU", "REINICIAR MENU")), new Color(0.08f, 0.35f, 0.65f, 1f)))
                    owner.OnRestartLevel1Click();
                if (DrawColoredButtonInRow(secondaryRow, 1, 3, owner.localizationManager.GetString("button", "openSaveFolder", LocalizationManager.Text("OPEN SAVE", "ABRIR SAVE")), new Color(0.08f, 0.35f, 0.65f, 1f)))
                    owner.OnOpenSaveFolderClick();
                if (DrawColoredButtonInRow(secondaryRow, 2, 3, owner.localizationManager.GetString("button", "openBackupFolder", LocalizationManager.Text("OPEN BACKUP", "ABRIR BACKUP")), new Color(0.08f, 0.35f, 0.65f, 1f)))
                    owner.OnOpenBackupFolderClick();
            }

            private void DrawRestorePointNameField()
            {
                string placeholder = owner.localizationManager.GetString("label", "restorePointName", LocalizationManager.Text("Restore Point Name (optional)", "Nome do ponto de restauração (opcional)"));
                Rect fieldRect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
                GUI.SetNextControlName("BackupSaveRestorePointName");
                restorePointName = GUI.TextField(fieldRect, restorePointName, 30, fieldStyle);
                bool fieldFocused = GUI.GetNameOfFocusedControl() == "BackupSaveRestorePointName";
                if (string.IsNullOrEmpty(restorePointName) && !fieldFocused)
                    GUI.Label(fieldRect, placeholder, placeholderFieldStyle);
            }

            private void DrawMeshsaveSection()
            {
                DrawSectionHeader(owner.localizationManager.GetString("header", "deleteMeshsave", LocalizationManager.Text("DELETE MESHSAVE FILE", "DELETAR ARQUIVO MESHSAVE")));
                GUILayout.Label(owner.localizationManager.GetString("text", "meshsaveInfo", LocalizationManager.Text("Delete the meshsave.txt file to restore the vehicle format.", "Delete o arquivo meshsave.txt para restaurar o formato do veículo.")), noteStyle);
                Rect deleteMeshsaveRect = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
                if (DrawOutlinedButton(deleteMeshsaveRect, owner.localizationManager.GetString("button", "deleteMeshsave", LocalizationManager.Text("DELETE MESHSAVE.TXT", "DELETAR MESHSAVE.TXT")), deleteButtonStyle, themeColor))
                    owner.OnDeleteMeshSaveClick();

                bool autoDelete = owner.GetAutoDeleteMeshsave();
                bool newAutoDelete = DrawLargeToggle(autoDelete, owner.localizationManager.GetString("label", "autoDeleteMeshsave", LocalizationManager.Text("Delete meshsave automatically", "Deletar meshsave automaticamente")));
                if (newAutoDelete != autoDelete)
                    owner.SetAutoDeleteMeshsave(newAutoDelete);
            }

            private void DrawImportSection()
            {
                if (!showImportSave && !showExternalImport)
                    return;

                DrawSectionHeader(owner.localizationManager.GetString("header", "importSave", LocalizationManager.Text("IMPORT SAVE", "IMPORTAR SAVE")));
                if (showImportSave)
                {
                    GUILayout.Label(owner.localizationManager.GetString("text", "importInfo", LocalizationManager.Text("Import your save from My Summer Car to My Winter Car with backup.", "Importe seu save de My Summer Car para My Winter Car com backup.")), noteStyle);
                    Rect importSaveRect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
                    if (DrawOutlinedButton(importSaveRect, owner.localizationManager.GetString("button", "importSave", LocalizationManager.Text("IMPORT SAVE FROM MY SUMMER CAR", "IMPORTAR SAVE DO MY SUMMER CAR")), buttonStyle, themeColor))
                    {
                        owner.OnImportSaveClick();
                        RefreshData();
                    }
                }

                if (showExternalImport)
                {
                    GUILayout.Label(owner.localizationManager.GetString("text", "importExternalPath", LocalizationManager.Text("Import external backups as restore points.", "Importar backups externos como pontos de restauração.")), noteStyle);
                    Rect importAllRect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
                    if (DrawOutlinedButton(importAllRect, owner.localizationManager.GetString("button", "importAllBackups", LocalizationManager.Text("IMPORT RESTORE POINTS", "IMPORTAR PONTOS DE RESTAURAÇÃO")), buttonStyle, themeColor))
                    {
                        owner.OnImportAllExternalBackupsClick();
                        RefreshData();
                    }
                }
            }

            private void DrawFooterActions(Rect footerRect)
            {
                Rect infoRect = new Rect(footerRect.x, footerRect.y, footerRect.width, 54f);
                Rect closeRect = new Rect(footerRect.x, footerRect.y + 61f, footerRect.width, 36f);

                if (DrawOutlinedButton(infoRect, owner.localizationManager.GetString("button", "info", LocalizationManager.Text("INFORMATION", "INFORMAÇÕES")), buttonStyle, themeColor))
                    ShowDetail(GetInfoTitle(), BuildInfoText());
                if (DrawOutlinedButton(closeRect, owner.localizationManager.GetString("button", "close", LocalizationManager.Text("CLOSE", "FECHAR")), buttonStyle, themeColor))
                    isVisible = false;
            }

            private void ShowDetail(string title, string text)
            {
                detailTitle = title;
                detailText = text;
                detailScroll = Vector2.zero;
            }

            private void DrawDetailOverlay(Rect panelRect)
            {
                if (string.IsNullOrEmpty(detailText))
                    return;

                float detailWidth = Mathf.Min(980f, panelRect.width - 80f);
                float detailHeight = Mathf.Min(360f, panelRect.height - 70f);
                Rect detailRect = new Rect(panelRect.x + (panelRect.width - detailWidth) * 0.5f, panelRect.y + (panelRect.height - detailHeight) * 0.5f, detailWidth, detailHeight);
                DrawBorderedPanel(detailRect, themeColor, 3f);
                GUILayout.BeginArea(new Rect(detailRect.x + 18f, detailRect.y + 14f, detailRect.width - 36f, detailRect.height - 28f));
                GUILayout.BeginHorizontal();
                GUILayout.Label(detailTitle, titleStyle, GUILayout.Height(50f), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("X", buttonStyle, GUILayout.Width(42f), GUILayout.Height(38f)))
                    detailText = "";
                GUILayout.EndHorizontal();
                detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUILayout.Label(detailText, detailTextStyle, GUILayout.ExpandWidth(true));
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            private string GetInfoTitle()
            {
                return owner.localizationManager.GetString("header", "titleInfo", LocalizationManager.Text("MOD INFORMATION", "INFORMAÇÕES DO MOD"));
            }

            private string BuildInfoText()
            {
                string text = owner.localizationManager.GetString("text", "infoTitle", LocalizationManager.Text("<b>BackupSave - Backup System</b>", "<b>BackupSave - Sistema de Backup</b>")) + "\n\n";
                text += owner.localizationManager.GetString("text", "infoOverview", LocalizationManager.Text(
                    "Creates compressed backups of your save and keeps restore tools available directly from the main menu.",
                    "Cria backups compactados do seu save e deixa as ferramentas de restauração direto no menu principal.")) + "\n\n";

                text += owner.localizationManager.GetString("text", "infoAutoBackup", LocalizationManager.Text(
                    "- Automatic backup: created when the save is loaded, preserving the current game state before you continue.",
                    "- Backup automático: criado quando o save é carregado, guardando o estado atual antes de continuar.")) + "\n";
                text += owner.localizationManager.GetString("text", "infoAutoRestore", LocalizationManager.Text(
                    "- Auto restore: detects when the save disappears after death and restores according to the selected mode.",
                    "- Restauração automática: detecta quando o save some após a morte e restaura conforme o modo escolhido.")) + "\n";
                text += owner.localizationManager.GetString("text", "infoRestorePoints", LocalizationManager.Text(
                    "- Restore points: permanent saves for important moments. They are not removed by the backup limit.",
                    "- Pontos de restauração: saves permanentes para momentos importantes. Eles não entram no limite de backups.")) + "\n";
                text += owner.localizationManager.GetString("text", "infoLimitControl", LocalizationManager.Text(
                    "- Backup limit: removes older automatic backups when the selected limit is exceeded. Use 0 for unlimited.",
                    "- Limite de backups: remove backups automáticos antigos quando passa do limite. Use 0 para ilimitado.")) + "\n";
                text += owner.localizationManager.GetString("text", "infoPrefixName", LocalizationManager.Text(
                    "- Name prefix: adds the character first name to new backups, useful when testing different saves.",
                    "- Prefixo de nome: adiciona o primeiro nome do personagem aos backups, útil para separar saves diferentes.")) + "\n";
                text += owner.localizationManager.GetString("text", "infoMeshsave", LocalizationManager.Text(
                    "- Meshsave tool: deletes meshsave.txt so the game can rebuild the vehicle format when needed.",
                    "- Ferramenta meshsave: deleta meshsave.txt para o jogo recriar o formato do veículo quando precisar.")) + "\n";
                text += owner.localizationManager.GetString("text", "infoImportSaveBackuper", LocalizationManager.Text(
                    "- External import: imports backups from SaveBackuper, MSC AutoBackup and MSC/MWC Save Backup Manager as restore points.",
                    "- Importação externa: importa backups do SaveBackuper, MSC AutoBackup e MSC/MWC Save Backup Manager como pontos de restauração.")) + "\n";
                if (ModLoader.CurrentGame == Game.MyWinterCar)
                    text += owner.localizationManager.GetString("text", "infoImportSaveMWC", LocalizationManager.Text(
                        "- MSC to MWC import: copies the My Summer Car save to My Winter Car and creates a safety backup first when possible.",
                        "- Importação MSC para MWC: copia o save do My Summer Car para My Winter Car e cria backup de segurança antes quando possível.")) + "\n";
                text += "\n" + owner.localizationManager.GetString("text", "infoFolders", LocalizationManager.Text(
                    "Use OPEN SAVE for the original game save folder and OPEN BACKUP for BackupSave backups.",
                    "Use ABRIR SAVE para a pasta original do jogo e ABRIR BACKUP para os backups do BackupSave."));
                return text;
            }

            private bool DrawColoredButtonInRow(Rect rowRect, int index, int count, string text, Color color)
            {
                const float gap = 4f;
                float width = (rowRect.width - gap * (count - 1)) / count;
                Rect buttonRect = new Rect(rowRect.x + index * (width + gap), rowRect.y, width, rowRect.height);

                return DrawOutlinedButton(buttonRect, text, buttonStyle, color);
            }

            private bool DrawOutlinedButton(Rect rect, string text, GUIStyle style, Color fillColor)
            {
                GUI.DrawTexture(rect, whiteTexture);
                Rect innerRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
                bool isPressed = Event.current != null && rect.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
                Rect drawRect = isPressed
                    ? new Rect(innerRect.x, innerRect.y + 2f, innerRect.width, innerRect.height - 2f)
                    : innerRect;

                Color oldBackground = GUI.backgroundColor;
                GUI.backgroundColor = fillColor;
                bool clicked = GUI.Button(drawRect, text, style);
                GUI.backgroundColor = oldBackground;
                return clicked;
            }

            private int DrawStepper(int value, int minValue, int maxValue, string displayText, bool wrap)
            {
                int result = value;

                Rect stepperRect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
                Rect leftRect = new Rect(stepperRect.x, stepperRect.y + 1f, 48f, 36f);
                Rect rightRect = new Rect(stepperRect.xMax - 48f, stepperRect.y + 1f, 48f, 36f);
                Rect valueRect = new Rect(leftRect.xMax + 4f, stepperRect.y + 1f, stepperRect.width - 104f, 36f);

                if (DrawOutlinedButton(leftRect, "<", stepperButtonStyle, themeColor))
                    result--;

                DrawBorderedPanel(valueRect, themeColor, 2f);
                GUI.Label(valueRect, displayText, stepperValueStyle);

                if (DrawOutlinedButton(rightRect, ">", stepperButtonStyle, themeColor))
                    result++;

                if (result < minValue)
                    return wrap ? maxValue : minValue;
                if (result > maxValue)
                    return wrap ? minValue : maxValue;
                return result;
            }

            private bool DrawLargeToggle(bool value, string text)
            {
                Rect rect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
                Rect boxRect = new Rect(rect.x, rect.y + 5f, 24f, 24f);
                Rect labelRect = new Rect(rect.x + 34f, rect.y, rect.width - 34f, rect.height);

                DrawBorderedPanel(boxRect, themeDarkColor, 2f);
                if (value)
                {
                    Color oldColor = GUI.color;
                    GUI.color = Color.yellow;
                    GUI.DrawTexture(new Rect(boxRect.x + 5f, boxRect.y + 5f, boxRect.width - 10f, boxRect.height - 10f), whiteTexture);
                    GUI.color = oldColor;
                }
                GUI.Label(labelRect, text, noteStyle);

                return GUI.Button(rect, GUIContent.none, GUIStyle.none) ? !value : value;
            }

            private void DrawBorderedPanel(Rect rect, Color fill, float border)
            {
                GUI.DrawTexture(rect, whiteTexture);
                Color oldColor = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(rect.x + border, rect.y + border, rect.width - border * 2f, rect.height - border * 2f), whiteTexture);
                GUI.color = oldColor;
            }

            private void DrawSectionHeader(string text)
            {
                GUILayout.Label(text, sectionHeaderStyle, GUILayout.Height(30f), GUILayout.ExpandWidth(true));
            }

            private string GetSelectedItem()
            {
                string[] items = owner.GetMenuPanelBackupItems();
                if (items.Length == 0)
                    return "";
                ClampSelectedIndex(items);
                return items[selectedIndex];
            }

            private void ClampSelectedIndex()
            {
                ClampSelectedIndex(owner.GetMenuPanelBackupItems());
            }

            private void ClampSelectedIndex(string[] items)
            {
                if (items == null || items.Length == 0)
                {
                    selectedIndex = 0;
                    return;
                }
                selectedIndex = Mathf.Clamp(selectedIndex, 0, items.Length - 1);
            }

            private void InitStyles()
            {
                if (titleStyle != null)
                    return;

                themeColor = ModLoader.CurrentGame == Game.MySummerCar
                    ? new Color(0.43f, 0.11f, 0.04f, 0.98f)
                    : new Color(0.08f, 0.34f, 0.42f, 0.98f);
                themeDarkColor = ModLoader.CurrentGame == Game.MySummerCar
                    ? new Color(0.25f, 0.07f, 0.02f, 0.98f)
                    : new Color(0.04f, 0.21f, 0.27f, 0.98f);

                whiteTexture = MakeTexture(Color.white);
                overlayTexture = MakeTexture(new Color(0f, 0f, 0f, 1f));
                selectedTexture = MakeTexture(ModLoader.CurrentGame == Game.MySummerCar ? new Color(0.62f, 0.22f, 0.03f, 1f) : new Color(0.02f, 0.55f, 0.72f, 1f));
                listTexture = MakeTexture(themeColor);
                sectionTexture = MakeTexture(themeColor);
                rowTexture = MakeTexture(themeDarkColor);
                rowHoverTexture = MakeTexture(ModLoader.CurrentGame == Game.MySummerCar ? new Color(0.50f, 0.15f, 0.03f, 1f) : new Color(0.06f, 0.42f, 0.52f, 1f));
                actionTexture = MakeTexture(themeColor);
                scrollbarTrackTexture = MakeTexture(ModLoader.CurrentGame == Game.MySummerCar ? new Color(0.12f, 0.03f, 0.01f, 1f) : new Color(0.02f, 0.13f, 0.17f, 1f));
                scrollbarThumbTexture = MakeTexture(ModLoader.CurrentGame == Game.MySummerCar ? new Color(0.72f, 0.22f, 0.03f, 1f) : new Color(0.02f, 0.62f, 0.78f, 1f));
                ApplyScrollbarStyle();

                listBoxStyle = new GUIStyle(GUI.skin.box);
                listBoxStyle.normal.background = listTexture;
                listBoxStyle.padding = new RectOffset(6, 6, 6, 6);

                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 34;
                titleStyle.fontStyle = FontStyle.BoldAndItalic;
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.normal.textColor = Color.yellow;

                titleShadowStyle = new GUIStyle(titleStyle);
                titleShadowStyle.normal.textColor = Color.black;

                sectionHeaderStyle = new GUIStyle(GUI.skin.label);
                sectionHeaderStyle.fontSize = 16;
                sectionHeaderStyle.fontStyle = FontStyle.BoldAndItalic;
                sectionHeaderStyle.alignment = TextAnchor.MiddleCenter;
                sectionHeaderStyle.normal.textColor = Color.yellow;
                sectionHeaderStyle.normal.background = sectionTexture;

                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 14;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.richText = true;
                labelStyle.normal.textColor = Color.white;

                noteStyle = new GUIStyle(GUI.skin.label);
                noteStyle.fontSize = 14;
                noteStyle.wordWrap = true;
                noteStyle.richText = true;
                noteStyle.normal.textColor = Color.white;

                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 15;
                buttonStyle.fontStyle = FontStyle.BoldAndItalic;
                buttonStyle.alignment = TextAnchor.MiddleCenter;
                buttonStyle.richText = true;
                buttonStyle.normal.textColor = Color.yellow;
                buttonStyle.hover.textColor = Color.yellow;
                buttonStyle.active.textColor = Color.yellow;
                buttonStyle.normal.background = actionTexture;
                buttonStyle.hover.background = rowHoverTexture;
                buttonStyle.active.background = selectedTexture;

                deleteButtonStyle = new GUIStyle(buttonStyle);
                deleteButtonStyle.normal.textColor = Color.red;
                deleteButtonStyle.hover.textColor = Color.red;
                deleteButtonStyle.active.textColor = Color.red;

                itemStyle = new GUIStyle(GUI.skin.button);
                itemStyle.fontSize = 15;
                itemStyle.alignment = TextAnchor.MiddleLeft;
                itemStyle.richText = true;
                itemStyle.normal.textColor = Color.white;
                itemStyle.hover.textColor = Color.yellow;
                itemStyle.active.textColor = Color.yellow;
                itemStyle.normal.background = rowTexture;
                itemStyle.hover.background = rowHoverTexture;
                itemStyle.active.background = selectedTexture;

                selectedItemStyle = new GUIStyle(itemStyle);
                selectedItemStyle.normal.background = selectedTexture;
                selectedItemStyle.hover.background = selectedTexture;
                selectedItemStyle.active.background = selectedTexture;

                fieldStyle = new GUIStyle(GUI.skin.textField);
                fieldStyle.fontSize = 14;
                fieldStyle.alignment = TextAnchor.MiddleLeft;
                fieldStyle.normal.textColor = Color.white;
                fieldStyle.hover.textColor = Color.white;
                fieldStyle.active.textColor = Color.white;
                fieldStyle.normal.background = rowTexture;
                fieldStyle.hover.background = rowTexture;
                fieldStyle.active.background = rowTexture;

                placeholderFieldStyle = new GUIStyle(fieldStyle);
                placeholderFieldStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
                placeholderFieldStyle.hover.textColor = placeholderFieldStyle.normal.textColor;
                placeholderFieldStyle.active.textColor = placeholderFieldStyle.normal.textColor;
                placeholderFieldStyle.padding = new RectOffset(fieldStyle.padding.left + 4, fieldStyle.padding.right, fieldStyle.padding.top, fieldStyle.padding.bottom);

                stepperButtonStyle = new GUIStyle(buttonStyle);
                stepperButtonStyle.richText = false;
                stepperButtonStyle.fontSize = 22;
                stepperButtonStyle.fontStyle = FontStyle.Bold;

                stepperValueStyle = new GUIStyle(fieldStyle);
                stepperValueStyle.alignment = TextAnchor.MiddleCenter;
                stepperValueStyle.fontSize = 15;
                stepperValueStyle.fontStyle = FontStyle.Bold;
                stepperValueStyle.normal.textColor = Color.white;

                detailTextStyle = new GUIStyle(noteStyle);
                detailTextStyle.fontSize = 16;
                detailTextStyle.richText = true;
            }

            private void ApplyScrollbarStyle()
            {
                GUI.skin.verticalScrollbar.normal.background = scrollbarTrackTexture;
                GUI.skin.verticalScrollbar.hover.background = scrollbarTrackTexture;
                GUI.skin.verticalScrollbar.active.background = scrollbarTrackTexture;
                GUI.skin.verticalScrollbar.fixedWidth = 14f;
                GUI.skin.verticalScrollbar.margin = new RectOffset(4, 0, 0, 0);
                GUI.skin.verticalScrollbar.padding = new RectOffset(2, 2, 2, 2);

                GUI.skin.verticalScrollbarThumb.normal.background = scrollbarThumbTexture;
                GUI.skin.verticalScrollbarThumb.hover.background = selectedTexture;
                GUI.skin.verticalScrollbarThumb.active.background = selectedTexture;
                GUI.skin.verticalScrollbarThumb.fixedWidth = 10f;

                GUI.skin.horizontalScrollbar.normal.background = scrollbarTrackTexture;
                GUI.skin.horizontalScrollbar.hover.background = scrollbarTrackTexture;
                GUI.skin.horizontalScrollbar.active.background = scrollbarTrackTexture;
                GUI.skin.horizontalScrollbar.fixedHeight = 12f;
                GUI.skin.horizontalScrollbar.margin = new RectOffset(0, 0, 4, 0);
                GUI.skin.horizontalScrollbar.padding = new RectOffset(2, 2, 2, 2);

                GUI.skin.horizontalScrollbarThumb.normal.background = scrollbarThumbTexture;
                GUI.skin.horizontalScrollbarThumb.hover.background = selectedTexture;
                GUI.skin.horizontalScrollbarThumb.active.background = selectedTexture;
                GUI.skin.horizontalScrollbarThumb.fixedHeight = 8f;
            }

            private Texture2D MakeTexture(Color color)
            {
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, color);
                texture.Apply();
                return texture;
            }

            private void OnDestroy()
            {
                DestroyTexture(overlayTexture);
                DestroyTexture(selectedTexture);
                DestroyTexture(listTexture);
                DestroyTexture(sectionTexture);
                DestroyTexture(whiteTexture);
                DestroyTexture(rowTexture);
                DestroyTexture(rowHoverTexture);
                DestroyTexture(actionTexture);
                DestroyTexture(scrollbarTrackTexture);
                DestroyTexture(scrollbarThumbTexture);
            }

            private void DestroyTexture(Texture2D texture)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            private class MenuButtonClickHandler : MonoBehaviour
            {
                private Action callback;
                private Vector3 originalScale;

                public void SetCallback(Action clickCallback)
                {
                    callback = clickCallback;
                    originalScale = transform.localScale;
                }

                private void Start()
                {
                    originalScale = transform.localScale;
                }

                private void OnMouseEnter()
                {
                    if (originalScale == Vector3.zero)
                        originalScale = transform.localScale;
                    transform.localScale = originalScale * 0.95f;
                }

                private void OnMouseExit()
                {
                    if (originalScale != Vector3.zero)
                        transform.localScale = originalScale;
                }

                private void OnMouseDown()
                {
                    if (callback != null && !IsMSCLoaderMenuOpen())
                        callback();
                }

                private bool IsMSCLoaderMenuOpen()
                {
                    try
                    {
                        MonoBehaviour[] behaviours = UnityEngine.Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                        for (int i = 0; i < behaviours.Length; i++)
                        {
                            MonoBehaviour behaviour = behaviours[i];
                            if (behaviour == null)
                                continue;

                            Type type = behaviour.GetType();
                            if (type == null || type.FullName != "MSCLoader.ModMenuButton")
                                continue;

                            FieldInfo openedField = type.GetField("opened", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (openedField != null && openedField.FieldType == typeof(bool) && (bool)openedField.GetValue(behaviour))
                                return true;
                        }
                    }
                    catch { }

                    return false;
                }
            }
        }

        private void Mod_OnLoad()
        {
            // Verificar se o mod foi inicializado corretamente
            if (autoRestoreManager == null || backupManager == null || localizationManager == null)
            {
                ModConsole.Error(LocalizationManager.Text(
                    "[BackupSave] Critical error: mod components were not initialized!",
                    "[BackupSave] Erro crítico: Componentes do mod não foram inicializados!"));
                return;
            }
            
            string gameFolder = GetGameSaveFolder();
            autoRestoreManager.SetAutoRestoreMode(GetAutoRestoreMode());
            if (GetPrefixCharacterName())
            {
                string characterName = backupManager.GetCharacterFirstName(gameFolder);
                if (!string.IsNullOrEmpty(characterName))
                {
                    autoRestoreManager.SetCharacterFirstName(characterName);
                }
            }
            autoRestoreManager.InitializeAutoRestore(gameFolder, "", GetBackupLimit());
        }

        private void ModUpdate()
        {
            ProcessMenuLocalizationReapply();

            string gameFolder = GetGameSaveFolder();
            int currentMode = GetAutoRestoreMode();
            autoRestoreManager.SetAutoRestoreMode(currentMode);
            autoRestoreManager.MonitorPlayerDeath(gameFolder);
            if (UnityEngine.Application.loadedLevel == 1)
            {
                EnsureMenuBackupPanel();
            }
            else
            {
                autoMeshsaveDeleteAttemptedInMenu = false;
                if (menuBackupPanel != null)
                    menuBackupPanel.Hide();
            }
            if (UnityEngine.Application.loadedLevel == 1 && GetAutoDeleteMeshsave() && !autoMeshsaveDeleteAttemptedInMenu)
            {
                autoMeshsaveDeleteAttemptedInMenu = true;
                backupManager.DeleteMeshSaveFile(gameFolder);
            }
        }

        private void OnMenuLoad()
        {
            if (autoRestoreManager == null || localizationManager == null)
                return;
                
            string gameFolder = GetGameSaveFolder();
            autoMeshsaveDeleteAttemptedInMenu = false;
            autoRestoreManager.CompleteAutoRestore(gameFolder);
            EnsureMenuBackupPanel();
        }

    }
}
