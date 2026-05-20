using MSCLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace BackupSave
{
    public class SaveManager : Mod
    {
        private const string LOG_PREFIX = "[BackupSave] ";
        private const string NO_BACKUPS_COLOR = "#ffaa00";
        public override string ID => "BackupSave";
        public override string Name => "BackupSave";
        public override string Author => "LucasMonOficial";
        public override string Version => "1.0.1";
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
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.Update, ModUpdate);
            SetupFunction(Setup.OnMenuLoad, OnMenuLoad);
            
            string gameFolder = GetGameSaveFolder();
            localizationManager = new LocalizationManager(gameFolder);
            backupManager = new BackupManager(MSCSaves, localizationManager);
            saveImportManager = new SaveImportManager(MSCSaves, backupManager, localizationManager);
            autoRestoreManager = new AutoRestoreManager(backupManager, MSCSaves, localizationManager);
        }

        private SettingsSliderInt backupLimitSlider;
        private SettingsDropDownList SavesList;
        private SettingsSliderInt autoRestoreModeSlider;
        private SettingsCheckBox autoDeleteMeshsaveCheckBox;
        private SettingsCheckBox prefixCharacterNameCheckBox;
        
        // Listas de backups/restore points carregadas no Mod_OnLoad (consolidadas)
        private Dictionary<string, string[]> backupLists = new Dictionary<string, string[]>();
        private bool listsLoaded = false;
        private bool autoMeshsaveDeleteAttemptedInMenu = false;
        
        // Performance cache (OTIMIZAÇÃO)
        private string[] backupLimitValuesCache = null;
        
        // Rastrear mudança no slider de Modo de Restauração para logar quando altera
        private int lastAutoRestoreMode = -1; // Iniciado com -1 para não fazer log na primeira carga
        
        private string GetGameSaveFolder()
            => ModLoader.CurrentGame == Game.MySummerCar ? "My Summer Car" : "My Winter Car";


        private int GetBackupLimit()
            => backupLimitSlider != null ? backupLimitSlider.GetValue() : 50;

        private int GetAutoRestoreMode()
            => autoRestoreModeSlider != null ? autoRestoreModeSlider.GetValue() : 1;

        private bool GetAutoDeleteMeshsave()
            => autoDeleteMeshsaveCheckBox != null ? autoDeleteMeshsaveCheckBox.GetValue() : false;

        private bool GetPrefixCharacterName()
            => prefixCharacterNameCheckBox != null ? prefixCharacterNameCheckBox.GetValue() : false;

        // OTIMIZAÇÃO: Cache GenerateBackupLimitValues para evitar alocação a cada Mod_Settings
        private string[] GenerateBackupLimitValues()
        {
            if (backupLimitValuesCache != null)
                return backupLimitValuesCache;
            
            string[] values = new string[101];
            values[0] = LocalizationManager.Text("Unlimited Backups", "Backups Ilimitados");
            if (localizationManager != null)
            {
                values[0] = localizationManager.GetString("label", "unlimitedBackups", values[0]);
            }
            for (int i = 1; i <= 100; i++)
            {
                values[i] = i.ToString();
            }
            backupLimitValuesCache = values;
            return values;
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
            UpdateSavesDropdownItems();
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

        private void UpdateSavesDropdownItems()
        {
            if (SavesList == null)
                return;

            string[] items = GetSavesDropdownItems(GetGameSaveFolder());

            try
            {
                Type listType = typeof(SettingsDropDownList);
                FieldInfo itemsField = listType.GetField("ArrayOfItems", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo valueField = listType.GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo defaultValueField = listType.GetField("DefaultValue", BindingFlags.Instance | BindingFlags.NonPublic);

                if (itemsField != null) itemsField.SetValue(SavesList, items);
                if (valueField != null) valueField.SetValue(SavesList, 0);
                if (defaultValueField != null) defaultValueField.SetValue(SavesList, 0);

                UpdateVisibleDropDownItems(items);
                SavesList.SetSelectedItemIndex(0);
            }
            catch (Exception ex)
            {
                ModConsole.Error(LocalizationManager.Text(
                    LOG_PREFIX + "Error updating backup list: ",
                    LOG_PREFIX + "Erro ao atualizar lista de backups: ") + ex.Message);
            }
        }

        private void UpdateVisibleDropDownItems(string[] items)
        {
            FieldInfo settingsElementField = typeof(ModSetting).GetField("SettingsElement", BindingFlags.Instance | BindingFlags.NonPublic);
            object settingsElement = settingsElementField != null ? settingsElementField.GetValue(SavesList) : null;
            if (settingsElement == null)
                return;

            FieldInfo dropDownField = settingsElement.GetType().GetField("dropDownList", BindingFlags.Instance | BindingFlags.Public);
            object dropDown = dropDownField != null ? dropDownField.GetValue(settingsElement) : null;
            if (dropDown == null)
                return;

            Type itemType = Type.GetType("MSCLoader.DropDownListItem, MSCLoader");
            if (itemType == null)
                return;

            Type genericListType = typeof(List<>).MakeGenericType(itemType);
            IList dropDownItems = (IList)Activator.CreateInstance(genericListType);
            ConstructorInfo constructor = itemType.GetConstructor(new Type[] { typeof(string), typeof(string), typeof(Sprite), typeof(bool), typeof(Action) });

            foreach (string item in items)
            {
                object dropDownItem = constructor != null
                    ? constructor.Invoke(new object[] { item, item, null, false, null })
                    : Activator.CreateInstance(itemType);
                dropDownItems.Add(dropDownItem);
            }

            FieldInfo visibleItemsField = dropDown.GetType().GetField("Items", BindingFlags.Instance | BindingFlags.Public);
            if (visibleItemsField != null)
                visibleItemsField.SetValue(dropDown, dropDownItems);

            MethodInfo rebuild = dropDown.GetType().GetMethod("RebuildPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo redraw = dropDown.GetType().GetMethod("RedrawPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo updateSelected = dropDown.GetType().GetMethod("UpdateSelected", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo selectedIndex = dropDown.GetType().GetProperty("SelectedIndex", BindingFlags.Instance | BindingFlags.Public);
            if (selectedIndex != null) selectedIndex.SetValue(dropDown, 0, null);
            if (rebuild != null) rebuild.Invoke(dropDown, null);
            if (redraw != null) redraw.Invoke(dropDown, null);
            if (updateSelected != null) updateSelected.Invoke(dropDown, null);
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

        private void Mod_Settings()
        {
            // Verificar se o mod foi inicializado corretamente
            if (localizationManager == null || backupManager == null)
            {
                ModConsole.Error(LocalizationManager.Text(
                    "[BackupSave] Error: Localization Manager or Backup Manager were not initialized!",
                    "[BackupSave] Erro: Localization Manager ou Backup Manager não inicializados!"));
                return;
            }
            
            // Garantir que as listas foram carregadas
            EnsureListsLoaded();
            
            string gameFolder = GetGameSaveFolder();

            // Header principal do mod com Informações e Créditos
            Settings.AddHeader(localizationManager.GetString("header", "backupsave", LocalizationManager.Text("BACKUPSAVE", "BACKUPSAVE")), new Color32(100, 100, 100, 255), new Color32(255, 255, 255, 255));
            Settings.CreateGroup(true);
            Settings.AddButton(localizationManager.GetString("button", "info", LocalizationManager.Text("<color=white>ℹ INFORMATION</color>", "<color=white>ℹ INFORMAÇÕES</color>")), new Action(OnShowModInfoClick));
            Settings.AddButton(localizationManager.GetString("button", "credits", LocalizationManager.Text("<color=yellow>★ CREDITS</color>", "<color=yellow>★ CRÉDITOS</color>")), new Action(OnShowCreditsClick));
            Settings.EndGroup();

            Settings.AddHeader(localizationManager.GetString("header", "settings", LocalizationManager.Text("SAVE SETTINGS", "CONFIGURAÇÕES DE SAVE")), new Color32(100, 100, 100, 255), new Color32(255, 255, 255, 255));
            
            Settings.AddText(localizationManager.GetString("text", "autoRestore", LocalizationManager.Text("<b>Automatic Restore Mode</b>\nThe mod detects when the save is deleted (death in Mortal Mode) and can automatically restore the latest backup.", "<b>Modo de Restauração Automática</b>\nO mod detecta quando o save é deletado (morte em Modo Mortal) e pode restaurar automaticamente o último backup.")));
            string[] autoRestoreModes = localizationManager.GetAutoRestoreModeValues();
            autoRestoreModeSlider = Settings.AddSlider("autoRestoreMode", localizationManager.GetString("label", "autoRestoreMode", LocalizationManager.Text("Restoration Mode", "Modo de Restauração")), 0, 2, 1, null, autoRestoreModes);
            
            Settings.AddText("____________________________________________________________________________________");
            
            Settings.AddText(localizationManager.GetString("text", "backupLimit", LocalizationManager.Text("<b>Backup Limit Configuration</b>\nSet the maximum number of backups to keep. Old backups will be deleted automatically.", "<b>Configuração de Limite de Backups</b>\nDefina o número máximo de backups a manter. Backups antigos serão deletados automaticamente.")));
            string[] backupLimitValues = GenerateBackupLimitValues();
            backupLimitSlider = Settings.AddSlider("backupLimit", localizationManager.GetString("label", "backupLimit", LocalizationManager.Text("Maximum Number of Backups to Store", "Número Máximo de Backups para Armazenar")), 0, 100, 50, null, backupLimitValues);
            
            Settings.AddText("____________________________________________________________________________________");
            
            Settings.AddText(localizationManager.GetString("text", "prefixCharName", LocalizationManager.Text("<b>Character Name Prefix</b>\nPrefix backups with the first name of the character to identify them easily.", "<b>Prefixo do Nome do Personagem</b>\nPrefixe os backups com o primeiro nome do personagem para identificá-los facilmente.")));
            prefixCharacterNameCheckBox = Settings.AddCheckBox("prefixCharacterName", localizationManager.GetString("label", "prefixCharacterName", LocalizationManager.Text("Prefix backup with character name", "Prefixar backups com o nome do personagem")), false);
            
            Settings.AddText("____________________________________________________________________________________");
            
            Settings.AddText(localizationManager.GetString("text", "backupsAndPoints", LocalizationManager.Text("<b>Backups and Restore Points</b>\nSelect a backup or restore point to restore/delete", "<b>Backups e Pontos de Restauração</b>\nSelecione um backup ou ponto de restauração para restaurar/deletar")));
            string[] savesDropdownItems = GetSavesDropdownItems(gameFolder);
            if (savesDropdownItems.Length == 1 && IsNoBackupsItem(savesDropdownItems[0]))
            {
                Settings.AddText(localizationManager.GetString("text", "noBackups", LocalizationManager.Text("You don't have any backups or restore points yet.", "Você ainda não tem nenhum backup ou ponto de restauração.")));
            }
            SavesList = Settings.AddDropDownList("saveselect", localizationManager.GetString("label", "selectSave", LocalizationManager.Text("Select a save or point", "Selecione um save ou ponto")), savesDropdownItems, 0);
                
                // Add Restore, Delete and Restart buttons side-by-side using horizontal group
                Settings.CreateGroup(true);
                Settings.AddButton(localizationManager.GetString("button", "restore", LocalizationManager.Text("RESTORE", "RESTAURAR")), new Action(OnRestoreBackupClick));
                Settings.AddButton(localizationManager.GetString("button", "delete", LocalizationManager.Text("<color=red>DELETE</color>", "<color=red>DELETAR</color>")), new Action(OnDeleteBackupClick));
                Settings.AddButton(localizationManager.GetString("button", "restartMenu", LocalizationManager.Text("<color=cyan>RESTART MENU</color>", "<color=cyan>REINICIAR MENU</color>")), new Action(OnRestartLevel1Click));
                Settings.EndGroup();
                
                // Add Open Backup Folder button
                Settings.AddButton(localizationManager.GetString("button", "openBackupFolder", LocalizationManager.Text("<color=white>OPEN BACKUP FOLDER</color>", "<color=white>ABRIR PASTA DE BACKUPS</color>")), new Action(OnOpenBackupFolderClick), SettingsButton.ButtonIcon.Folder);
                
                Settings.AddText(localizationManager.GetString("text", "reloadTip", LocalizationManager.Text("Restore, create, or delete updates the list immediately.", "Restaurar, criar ou deletar atualiza a lista imediatamente.")));

             
            // Create Restore Point Section with header collapsed by default
            Settings.AddHeader(localizationManager.GetString("header", "createRestorePoint", LocalizationManager.Text("CREATE RESTORE POINT", "CRIAR PONTO DE RESTAURAÇÃO")), new Color32(100, 100, 100, 255), new Color32(255, 255, 255, 255), collapsedByDefault: true);
            SettingsTextBox restorePointInput = Settings.AddTextBox("restorePointName", localizationManager.GetString("label", "restorePointName", LocalizationManager.Text("Restore Point Name (optional)", "Nome do Ponto de Restauração (opcional)")), string.Empty, localizationManager.GetString("text", "restorePointPlaceholder", LocalizationManager.Text("Type the name here...", "Digite o nome aqui...")));
            Settings.AddButton(localizationManager.GetString("button", "createRestorePoint", LocalizationManager.Text("Create Restore Point", "Criar Ponto de Restauração")), new Action(() => OnCreateRestorePointClick(restorePointInput)));
            Settings.AddText(localizationManager.GetString("text", "restorePointInfo", LocalizationManager.Text("Restore points are permanent and will not be deleted by backup limits.", "Os pontos de restauração são permanentes e não serão excluídos pelos limites de backup.")));
            
            // Delete Meshsave Section with header collapsed by default
            Settings.AddHeader(localizationManager.GetString("header", "deleteMeshsave", LocalizationManager.Text("DELETE MESHSAVE FILE", "DELETAR ARQUIVO MESHSAVE")), new Color32(100, 100, 100, 255), new Color32(255, 255, 255, 255), collapsedByDefault: true);
            Settings.AddText(localizationManager.GetString("text", "meshsaveInfo", LocalizationManager.Text("Delete the meshsave.txt file to restore the vehicle format.", "Delete o arquivo meshsave.txt para restaurar o formato do veículo.")));
            Settings.CreateGroup(true);
            Settings.AddButton(localizationManager.GetString("button", "deleteMeshsave", LocalizationManager.Text("<color=red>Delete meshsave.txt</color>", "<color=red>Deletar meshsave.txt</color>")), new Action(OnDeleteMeshSaveClick));
            Settings.EndGroup();
            autoDeleteMeshsaveCheckBox = Settings.AddCheckBox("autoDeleteMeshsave", localizationManager.GetString("label", "autoDeleteMeshsave", LocalizationManager.Text("Delete meshsave automatically", "Deletar meshsave automaticamente")), false);

            // Save Import Button - Only show if in MWC with header collapsed by default
            if (ModLoader.CurrentGame == Game.MyWinterCar && saveImportManager.HasMSCSave())
            {
                Settings.AddHeader(localizationManager.GetString("header", "importSave", LocalizationManager.Text("IMPORT SAVE", "IMPORTAR SAVE")), new Color32(100, 100, 100, 255), new Color32(255, 255, 255, 255), collapsedByDefault: true);
                Settings.AddText(localizationManager.GetString("text", "importInfo", LocalizationManager.Text("Import your save from My Summer Car to My Winter Car with backup.", "Importe seu save de My Summer Car para My Winter Car com backup.")));
                Settings.AddButton(localizationManager.GetString("button", "importSave", LocalizationManager.Text("Import Save from My Summer Car", "Importar Save do My Summer Car")), new Action(OnImportSaveClick));
            }
            
            // External Backup Import Button - Only show if in MSC and external backups exist
            if (ModLoader.CurrentGame == Game.MySummerCar && saveImportManager.HasExternalBackups())
            {
                Settings.AddHeader(localizationManager.GetString("header", "importExternalBackups", LocalizationManager.Text("IMPORT SAVEBACKUPER BACKUPS", "IMPORTAR BACKUPS DE SAVEBACKUPER")), new Color32(100, 100, 100, 255), new Color32(255, 255, 255, 255), collapsedByDefault: true);
                Settings.AddText(localizationManager.GetString("text", "importExternalPath", LocalizationManager.Text("Import all backups from: C:\\Users\\{user}\\AppData\\LocalLow\\Amistech\\Backup", "Importar todos os backups de: C:\\Users\\{user}\\AppData\\LocalLow\\Amistech\\Backup")));
                Settings.AddButton(localizationManager.GetString("button", "importAllBackups", LocalizationManager.Text("Import All Backups", "Importar Todos os Backups")), new Action(() => OnImportAllExternalBackupsClick()));
                Settings.AddText(localizationManager.GetString("text", "importedPrefix", LocalizationManager.Text("Imported backups will receive the 'IMPORTED - ' prefix for better identification.", "Backups importados receberao o prefixo 'IMPORTADO - ' para melhor identificação.")));
            }
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

        private void OnRestoreBackupClick()
        {
            if (SavesList == null) return;
            string selectedName = SavesList.GetSelectedItemName();
            if (IsNoBackupsItem(selectedName))
            {
                ShowPopup(
                    localizationManager.GetString("text", "noBackups", LocalizationManager.Text("You don't have any backups or restore points yet.", "Você ainda não tem nenhum backup ou ponto de restauração.")),
                    "",
                    "#ffaa00",
                    localizationManager.GetString("popup", "titleWarning", LocalizationManager.Text("WARNING", "AVISO")));
                return;
            }
            string cleanName = GetCleanBackupName(selectedName);
            bool isRestorePoint = IsRestorePointItem(selectedName);
            bool success = isRestorePoint ? 
                backupManager.RestoreRestorePointByName(GetGameSaveFolder(), cleanName) :
                backupManager.RestoreBackupByName(GetGameSaveFolder(), cleanName);
            string successMsg = isRestorePoint
                ? localizationManager.GetString("popup", "successRestorePoint", LocalizationManager.Text("Your restore point has been successfully restored!", "Seu ponto de restauração foi restaurado com sucesso!"))
                : localizationManager.GetString("popup", "successRestoreBackup", LocalizationManager.Text("Your backup has been successfully restored!", "Seu backup foi restaurado com sucesso!"));
            string failMsg = isRestorePoint
                ? localizationManager.GetString("popup", "failRestorePoint", LocalizationManager.Text("Failed to restore the restore point.", "Falha ao restaurar o ponto de restauração."))
                : localizationManager.GetString("popup", "failRestoreBackup", LocalizationManager.Text("Failed to restore the backup.", "Falha ao restaurar o backup."));
            ShowPopup(success ? successMsg : failMsg, cleanName, success ? (isRestorePoint ? "#ff9900" : "#00ff00") : "#ff0000", success ? localizationManager.GetString("popup", "titleSuccess", LocalizationManager.Text("SUCCESS", "SUCESSO")) : localizationManager.GetString("popup", "titleFailure", LocalizationManager.Text("FAILURE", "FALHA")));
        }

        private void OnDeleteBackupClick()
        {
            if (SavesList == null) return;
            string gameFolder = GetGameSaveFolder();
            string selectedName = SavesList.GetSelectedItemName();
            if (IsNoBackupsItem(selectedName))
            {
                ShowPopup(
                    localizationManager.GetString("text", "noBackups", LocalizationManager.Text("You don't have any backups or restore points yet.", "Você ainda não tem nenhum backup ou ponto de restauração.")),
                    "",
                    "#ffaa00",
                    localizationManager.GetString("popup", "titleWarning", LocalizationManager.Text("WARNING", "AVISO")));
                return;
            }
            string cleanName = GetCleanBackupName(selectedName);
            bool isRestorePoint = IsRestorePointItem(selectedName);
            bool success = isRestorePoint ? 
                backupManager.DeleteRestorePointByName(gameFolder, cleanName) :
                backupManager.DeleteBackupByName(gameFolder, cleanName);
            if (success)
                RefreshBackupLists();
            string successMsg = isRestorePoint
                ? localizationManager.GetString("popup", "successDeletePointMsg", LocalizationManager.Text("Restore point was successfully deleted!", "Ponto de restauração foi deletado com sucesso!"))
                : localizationManager.GetString("popup", "successDeleteBackupMsg", LocalizationManager.Text("Backup was successfully deleted!", "Backup foi deletado com sucesso!"));
            string failMsg = isRestorePoint
                ? localizationManager.GetString("popup", "failDeletePointMsg", LocalizationManager.Text("Restore point was deleted.", "Ponto de restauração foi deletado."))
                : localizationManager.GetString("popup", "failDeleteBackupMsg", LocalizationManager.Text("Backup was deleted.", "Backup foi deletado."));
            ShowPopup(success ? successMsg : failMsg, cleanName, success ? (isRestorePoint ? "#ffaa00" : "#00ff00") : "#ff0000", success ? localizationManager.GetString("popup", "titleSuccess", LocalizationManager.Text("SUCCESS", "SUCESSO")) : localizationManager.GetString("popup", "titleFailure", LocalizationManager.Text("FAILURE", "FALHA")));
        }

        private void OnDeleteMeshSaveClick()
        {
            bool success = backupManager.DeleteMeshSaveFile(GetGameSaveFolder());
            string successMsg = localizationManager.GetString("popup", "successDeleteMeshsaveMsg", LocalizationManager.Text("Meshsave.txt file was successfully deleted!", "Arquivo meshsave.txt foi deletado com sucesso!"));
            string failMsg = localizationManager.GetString("popup", "notFoundMeshsave", LocalizationManager.Text("Meshsave.txt file not found.", "Arquivo meshsave.txt não foi encontrado."));
            string titleWarning = localizationManager.GetString("popup", "titleWarning", LocalizationManager.Text("WARNING", "AVISO"));
            ShowPopup(success ? successMsg : failMsg, "", success ? "#00ff00" : "#ffaa00", success ? localizationManager.GetString("popup", "titleSuccess", LocalizationManager.Text("SUCCESS", "SUCESSO")) : titleWarning);
        }

        private void OnCreateRestorePointClick(SettingsTextBox restorePointInput)
        {
            string gameFolder = GetGameSaveFolder();
            string restorePointName = restorePointInput != null ? restorePointInput.GetValue() : "";
            string createdPointName = backupManager.CreateRestorePoint(gameFolder, restorePointName);
            if (!string.IsNullOrEmpty(createdPointName))
            {
                RefreshBackupLists();
                ShowPopup(
                    localizationManager.GetString("popup", "successCreatePointMsg", LocalizationManager.Text("Restore point was successfully created!", "Ponto de restauração foi criado com sucesso!")),
                    createdPointName,
                    "#ff9900",
                    localizationManager.GetString("popup", "titleSuccess", LocalizationManager.Text("SUCCESS", "SUCESSO")));
            }
            else
                ShowPopup(
                    localizationManager.GetString("popup", "failCreatePointMsg", LocalizationManager.Text("Failed to create restore point!\n\nNo valid save file was found.", "Falha ao criar ponto de restauração!\n\nNenhum arquivo de save válido foi encontrado.")),
                    "",
                    "#ff0000",
                    localizationManager.GetString("popup", "titleFailure", LocalizationManager.Text("FAILURE", "FALHA")));
        }

        private void OnRestartLevel1Click()
        {
            string restartingMessage = LocalizationManager.Text("[BackupSave] Restarting menu", "[BackupSave] Reiniciando o menu");
            ModConsole.Log(localizationManager.GetString("log", "restartingMenu", restartingMessage));
            UnityEngine.Application.LoadLevel(1);
        }

        private void OnShowModInfoClick()
        {
            string infoText = localizationManager.GetString("text", "infoTitle", LocalizationManager.Text("<b>BackupSave - Backup System</b>", "<b>BackupSave - Sistema de Backup</b>")) + "\n\n" + localizationManager.GetString("text", "infoFeatures", LocalizationManager.Text("<b>Features:</b>", "<b>Recursos:</b>")) + "\n";
            infoText += localizationManager.GetString("text", "infoAutoBackup", LocalizationManager.Text("• <b>Automatic backups:</b> Every time you load the game a backup is created", "• <b>Backups automáticos:</b> Toda vez que carrega o jogo um backup é criado")) + "\n";
            infoText += localizationManager.GetString("text", "infoAutoRestore", LocalizationManager.Text("• <b>Automatic restore:</b> Detects death and restores the latest backup automatically", "• <b>Restauração automática:</b> Detecta morte e restaura o último backup automaticamente")) + "\n";
            infoText += localizationManager.GetString("text", "infoRestorePoints", LocalizationManager.Text("• <b>Restore Points:</b> Create permanent points to restore whenever you want", "• <b>Pontos de Restauração:</b> Crie pontos permanentes para restaurar sempre que quiser")) + "\n";
            infoText += localizationManager.GetString("text", "infoLimitControl", LocalizationManager.Text("• <b>Limit Control:</b> Set how many backups to keep", "• <b>Controle de Limite:</b> Defina quantos backups manter")) + "\n";
            infoText += localizationManager.GetString("text", "infoPrefixName", LocalizationManager.Text("• <b>Character Name Prefix:</b> Add the character's name to the backup name", "• <b>Prefixo do Personagem:</b> Adicione o nome do personagem ao nome do backup")) + "\n";
            infoText += localizationManager.GetString("text", "infoMeshsave", LocalizationManager.Text("• <b>Delete meshsave:</b> Reset vehicle format when needed", "• <b>Deletar meshsave:</b> Redefina o formato do veículo quando necessário")) + "\n";
            if (ModLoader.CurrentGame == Game.MySummerCar) infoText += localizationManager.GetString("text", "infoImportSaveBackuper", LocalizationManager.Text("• <b>Import saves from SaveBackuper:</b> Import your saves from the SaveBackuper MOD", "• <b>Importar saves do SaveBackuper:</b> Importe seus saves do MOD SaveBackuper")) + "\n";
            if (ModLoader.CurrentGame == Game.MyWinterCar) infoText += localizationManager.GetString("text", "infoImportSaveMWC", LocalizationManager.Text("• <b>Import Save:</b> Import your save from My Summer Car to My Winter Car", "• <b>Importar Save:</b> Importe seu save do My Summer Car para o My Winter Car")) + "\n";
            infoText += "\n" + localizationManager.GetString("text", "infoModes", LocalizationManager.Text("<b>Restoration Modes:</b>", "<b>Modos de Restauração:</b>")) + "\n";
            infoText += localizationManager.GetString("text", "infoDisabled", LocalizationManager.Text("• <b>Disabled:</b> Does not restore automatically", "• <b>Desligado:</b> Não restaura automaticamente")) + "\n";
            infoText += localizationManager.GetString("text", "infoRestoreAll", LocalizationManager.Text("• <b>Restore All:</b> Restores all save files", "• <b>Restaurar Tudo:</b> Restaura todos os arquivos do save")) + "\n";
            infoText += localizationManager.GetString("text", "infoGraveyard", LocalizationManager.Text("• <b>Restore Keeping Gravestones:</b> Restores but keeps the gravestones", "• <b>Restaurar Mantendo as Lápides:</b> Restaura mas mantém as lápides"));
            ModUI.ShowMessage(infoText, localizationManager.GetString("popup", "titleInfo", LocalizationManager.Text("MOD INFORMATION", "INFORMAÇÕES DO MOD")));
        }

        private void OnShowCreditsClick()
        {
            string creditsText = localizationManager.GetString("text", "creditsTitle", LocalizationManager.Text("<b>BackupSave - Credits</b>", "<b>BackupSave - Créditos</b>")) + "\n\n";
            creditsText += localizationManager.GetString("text", "creditsBasedOn", LocalizationManager.Text("<b>Based on:</b>", "<b>Baseado em:</b>")) + "\n";
            creditsText += localizationManager.GetString("text", "creditsSaveBackuper", LocalizationManager.Text("SaveBackuper by AnimeForevere", "SaveBackuper por AnimeForevere"));
            ModUI.ShowMessage(creditsText, localizationManager.GetString("popup", "titleCredits", LocalizationManager.Text("CREDITS", "CRÉDITOS")));
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

        private void ShowPopup(string message, string detailName = "", string color = "#00ff00", string title = "")
        {
            if (string.IsNullOrEmpty(title))
            {
                title = LocalizationManager.Text("SUCCESS", "SUCESSO");
            }
            string detail = string.IsNullOrEmpty(detailName) ? "" : "\n<color=" + color + ">" + detailName + "</color>";
            ModUI.ShowMessage(message + detail, title);
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
                    string prefixMessage = LocalizationManager.Text("[BackupSave] Character name prefix enabled: ", "[BackupSave] Prefixo do personagem ativado: ");
                    ModConsole.Log("<color=#00ff00>" + localizationManager.GetString("log", "prefixEnabled", prefixMessage) + characterName + "</color>");
                }
            }
            autoRestoreManager.InitializeAutoRestore(gameFolder, "", GetBackupLimit());
        }

        private void ModUpdate()
        {
            string gameFolder = GetGameSaveFolder();
            int currentMode = GetAutoRestoreMode();
            if (lastAutoRestoreMode >= 0 && currentMode != lastAutoRestoreMode)
            {
                string logColor = currentMode == 0 ? "#ff0000" : "#00ff00";
                string logKey = "";
                string defaultValue = "";
                
                switch (currentMode)
                {
                    case 0: 
                        logKey = "autoRestoreModeDisabled";
                        defaultValue = LocalizationManager.Text("[BackupSave] Automatic Restoration: Disabled", "[BackupSave] Restauração Automática: Desligada");
                        break;
                    case 1: 
                        logKey = "autoRestoreModeRestoreAll";
                        defaultValue = LocalizationManager.Text("[BackupSave] Automatic Restoration: Restore All", "[BackupSave] Restauração Automática: Restaurar Tudo");
                        break;
                    case 2: 
                        logKey = "autoRestoreModeRestoreGraveyard";
                        defaultValue = LocalizationManager.Text("[BackupSave] Automatic Restoration: Restore keeping gravestones", "[BackupSave] Restauração Automática: Restaurar mantendo as lápides");
                        break;
                }
                
                string logMessage = localizationManager.GetString("log", logKey, defaultValue);
                ModConsole.Log("<color=" + logColor + ">" + logMessage + "</color>");
            }
            lastAutoRestoreMode = currentMode;
            autoRestoreManager.SetAutoRestoreMode(currentMode);
            autoRestoreManager.MonitorPlayerDeath(gameFolder);
            if (UnityEngine.Application.loadedLevel != 1)
            {
                autoMeshsaveDeleteAttemptedInMenu = false;
            }
            else if (GetAutoDeleteMeshsave() && !autoMeshsaveDeleteAttemptedInMenu)
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
        }

    }
}
