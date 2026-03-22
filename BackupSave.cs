using MSCLoader;
using System;
using System.IO;
using System.Reflection;

namespace BackupSave
{
    public class SaveManager : Mod
    {
        private const string LOG_PREFIX = "[BackupSave] ";
        public override string ID => "BackupSave";
        public override string Name => "BackupSave";
        public override string Author => "LucasMonOficial";
        public override string Version => "1.0.0";
        public override string Description => "Sistema avançado de backup automático com restauração fácil, controle de limite de backups.";
        public override Game SupportedGames => Game.MySummerCar | Game.MyWinterCar;

        public override byte[] Icon { get; set; }

        private BackupManager backupManager;
        private SaveImportManager saveImportManager;
        private AutoRestoreManager autoRestoreManager;
        readonly string MSCSaves = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow") + "/Amistech";

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.Update, ModUpdate);
            SetupFunction(Setup.OnMenuLoad, OnMenuLoad);
            
            backupManager = new BackupManager(MSCSaves);
            saveImportManager = new SaveImportManager(MSCSaves, backupManager);
            autoRestoreManager = new AutoRestoreManager(backupManager, MSCSaves);

            // Carregar ícone do assembly (embedded resource)
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "BackupSave.icone.png";
                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream != null)
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;
                            while ((bytesRead = resourceStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                memoryStream.Write(buffer, 0, bytesRead);
                            }
                            Icon = memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModConsole.Error(LOG_PREFIX + "Erro ao carregar ícone: " + ex.Message);
            }
        }
        
        private SettingsSliderInt backupLimitSlider;
        private SettingsDropDownList SavesList;
        private SettingsSliderInt autoRestoreModeSlider;
        private SettingsCheckBox autoDeleteMeshsaveCheckBox;
        private SettingsCheckBox prefixCharacterNameCheckBox;
        
        // Listas de backups/restore points carregadas no Mod_OnLoad
        private string[] mscBackupList = new string[] { };
        private string[] mwcBackupList = new string[] { };
        private string[] mscRestorePointList = new string[] { };
        private string[] mwcRestorePointList = new string[] { };
        private bool listsLoaded = false;
        
        // Performance cache (OTIMIZAÇÃO)
        private string[] backupLimitValuesCache = null;
        
        // Rastrear mudança no slider de Modo de Restauração para logar quando altera
        private int lastAutoRestoreMode = -1; // Iniciado com -1 para não fazer log na primeira carga
        
        // Constante compartilhada de modos de restauração
        private static readonly string[] MODE_NAMES = { "Desligada", "Restaurar Tudo", "Restaurar mantendo as lápides" };
        
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
            values[0] = "Backups Ilimitados";
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
            
            mscBackupList = backupManager.GetBackupList("My Summer Car");
            mwcBackupList = backupManager.GetBackupList("My Winter Car");
            mscRestorePointList = backupManager.GetRestorePointList("My Summer Car");
            mwcRestorePointList = backupManager.GetRestorePointList("My Winter Car");
            listsLoaded = true;
        }

        // Cria uma lista unificada de backups e restore points com cores
        private string[] GetUnifiedBackupAndRestorePointList(string gameFolder)
        {
            string[] backupList = gameFolder == "My Summer Car" ? mscBackupList : mwcBackupList;
            string[] restorePointList = gameFolder == "My Summer Car" ? mscRestorePointList : mwcRestorePointList;
            
            int totalCount = backupList.Length + restorePointList.Length;
            string[] unifiedList = new string[totalCount];
            
            // Copiar backups com cor verde
            for (int i = 0; i < backupList.Length; i++)
            {
                string backupName = backupList[i];
                // Se o nome não contém a tag de cor, adicionar verde
                if (!backupName.StartsWith("<color=#21ff13>"))
                {
                    backupName = "<color=#21ff13>" + backupName + "</color>";
                }
                unifiedList[i] = backupName;
            }
            
            // Copiar restore points com cor laranja
            for (int i = 0; i < restorePointList.Length; i++)
            {
                string restorePointName = restorePointList[i];
                // Se o nome não contém a tag de cor, adicionar laranja
                if (!restorePointName.StartsWith("<color=#ff9900>"))
                {
                    restorePointName = "<color=#ff9900>PR: " + restorePointName + "</color>";
                }
                unifiedList[backupList.Length + i] = restorePointName;
            }
            
            return unifiedList;
        }

        private void Mod_Settings()
        {
            // Garantir que as listas foram carregadas
            EnsureListsLoaded();
            
            string gameFolder = GetGameSaveFolder();

            // Header principal do mod com Informações e Créditos
            Settings.AddHeader("BACKUPSAVE");
            Settings.CreateGroup(true);
            Settings.AddButton("<color=cyan>ℹ INFORMAÇÕES</color>", new Action(OnShowModInfoClick));
            Settings.AddButton("<color=yellow>★ CRÉDITOS</color>", new Action(OnShowCreditsClick));
            Settings.EndGroup();

            Settings.AddHeader("CONFIGURAÇÕES DE SAVE");
            
            Settings.AddText("<b>Modo de Restauração Automática</b>\nO mod detecta quando o save é deletado (morte em Modo Mortal) e pode restaurar automaticamente o último backup.");
            autoRestoreModeSlider = Settings.AddSlider("autoRestoreMode", "Modo de Restauração", 0, 2, 1, null, MODE_NAMES);
            
            Settings.AddText("____________________________________________________________________________________");
            
            Settings.AddText("<b>Configuração de Limite de Backups</b>\nDefina o número máximo de backups a manter. Backups antigos serão deletados automaticamente.");
            string[] backupLimitValues = GenerateBackupLimitValues();
            backupLimitSlider = Settings.AddSlider("backupLimit", "Número Máximo de Backups para Armazenar", 0, 100, 50, null, backupLimitValues);
            
            Settings.AddText("____________________________________________________________________________________");
            
            Settings.AddText("<b>Prefixo do Nome do Personagem</b>\nPrefixe os backups com o primeiro nome do personagem para identificá-los facilmente.");
            prefixCharacterNameCheckBox = Settings.AddCheckBox("prefixCharacterName", "Prefixar backups com o nome do personagem", false);
            
            Settings.AddText("____________________________________________________________________________________");
            
            Settings.AddText("<b>Backups e Pontos de Restauração</b>\nSelecione um backup ou ponto de restauração para restaurar/deletar");
            string[] unifiedList = GetUnifiedBackupAndRestorePointList(gameFolder);
            if (unifiedList.Length == 0)
            {
                Settings.AddText("Você ainda não tem nenhum backup ou ponto de restauração.");
            }
            else
            {
                SavesList = Settings.AddDropDownList("saveselect", "Selecione um save ou ponto", unifiedList, 0);
                
                // Add Restore, Delete and Restart buttons side-by-side using horizontal group
                Settings.CreateGroup(true);
                Settings.AddButton("RESTAURAR", new Action(OnRestoreBackupClick));
                Settings.AddButton("<color=red>DELETAR</color>", new Action(OnDeleteBackupClick));
                Settings.AddButton("<color=cyan>REINICIAR MENU</color>", new Action(OnRestartLevel1Click));
                Settings.EndGroup();
                
                Settings.AddText("Restaurar ou deletar são aplicadas imediatamente. Recarregue o jogo para atualizar a lista de saves.");
            }

            
            // Create Restore Point Section with header collapsed by default
            Settings.AddHeader("CRIAR PONTO DE RESTAURAÇÃO", collapsedByDefault: true);
            SettingsTextBox restorePointInput = Settings.AddTextBox("restorePointName", "Nome do Ponto de Restauração (opcional)", string.Empty, "Digite o nome aqui...");
            Settings.AddButton("Criar Ponto de Restauração", new Action(() => OnCreateRestorePointClick(restorePointInput)));
            Settings.AddText("Os pontos de restauração são permanentes e não serão excluídos pelos limites de backup.");
            
            // Delete Meshsave Section with header collapsed by default
            Settings.AddHeader("DELETAR ARQUIVO MESHSAVE", collapsedByDefault: true);
            Settings.AddText("Delete o arquivo meshsave.txt para restaurar o formato do veículo.");
            Settings.CreateGroup(true);
            Settings.AddButton("<color=red>Deletar meshsave.txt</color>", new Action(OnDeleteMeshSaveClick));
            Settings.EndGroup();
            autoDeleteMeshsaveCheckBox = Settings.AddCheckBox("autoDeleteMeshsave", "Deletar meshsave automaticamente", false);

            // Save Import Button - Only show if in MWC with header collapsed by default
            if (ModLoader.CurrentGame == Game.MyWinterCar && saveImportManager.HasMSCSave())
            {
                Settings.AddHeader("IMPORTAR SAVE", collapsedByDefault: true);
                Settings.AddText("Importe seu save de My Summer Car para My Winter Car com backup.");
                Settings.AddButton("Importar Save do My Summer Car", new Action(OnImportSaveClick));
            }
            
            // External Backup Import Button - Only show if in MSC and external backups exist
            if (ModLoader.CurrentGame == Game.MySummerCar && saveImportManager.HasExternalBackups())
            {
                Settings.AddHeader("IMPORTAR BACKUPS DE SAVEBACKUPER", collapsedByDefault: true);
                Settings.AddText("Importar todos os backups de: C:\\Users\\{user}\\AppData\\LocalLow\\Amistech\\Backup");
                Settings.AddButton("Importar Todos os Backups", new Action(() => OnImportAllExternalBackupsClick()));
                Settings.AddText("Backups importados receberao o prefixo 'IMPORTADO - ' para melhor identificação.");
            }
        }

        // Verifica se um item da lista unificada é um ponto de restauração
        private bool IsRestorePointItem(string itemName)
        {
            return itemName.StartsWith("<color=#ff9900>");
        }

        // Remove as tags de cor e prefixo "PR: " se existir
        private string GetCleanBackupName(string itemName)
        {
            // Remove tags de cor
            string cleaned = itemName.Replace("<color=#ff9900>", "").Replace("<color=#21ff13>", "").Replace("</color>", "");
            // Remove prefixo "PR: " se for restore point
            if (cleaned.StartsWith("PR: "))
                cleaned = cleaned.Substring("PR: ".Length);
            return cleaned;
        }

        private void OnRestoreBackupClick()
        {
            if (SavesList == null) return;
            string selectedName = SavesList.GetSelectedItemName();
            string cleanName = GetCleanBackupName(selectedName);
            bool isRestorePoint = IsRestorePointItem(selectedName);
            bool success = isRestorePoint ? 
                backupManager.RestoreRestorePointByName(GetGameSaveFolder(), cleanName) :
                backupManager.RestoreBackupByName(GetGameSaveFolder(), cleanName);
            ShowPopup(success ? (isRestorePoint ? "Seu ponto de restauração foi restaurado com sucesso!" : "Seu backup foi restaurado com sucesso!") : "Falha ao restaurar", 
                cleanName, success ? "#00ff00" : "#ff0000", success ? "SUCESSO" : "FALHA");
        }

        private void OnDeleteBackupClick()
        {
            if (SavesList == null) return;
            string gameFolder = GetGameSaveFolder();
            string selectedName = SavesList.GetSelectedItemName();
            if (selectedName == null) return;
            string cleanName = GetCleanBackupName(selectedName);
            bool isRestorePoint = IsRestorePointItem(selectedName);
            bool success = isRestorePoint ? 
                backupManager.DeleteRestorePointByName(gameFolder, cleanName) :
                backupManager.DeleteBackupByName(gameFolder, cleanName);
            ShowPopup(success ? (isRestorePoint ? "Ponto de restauração foi deletado com sucesso!" : "Backup foi deletado com sucesso!") : (isRestorePoint ? "Falha ao deletar o ponto" : "Falha ao deletar o backup"), 
                cleanName, success ? "#ffaa00" : "#ff0000", success ? "SUCESSO" : "FALHA");
        }

        private void OnDeleteMeshSaveClick()
        {
            bool success = backupManager.DeleteMeshSaveFile(GetGameSaveFolder());
            ShowPopup(success ? "Arquivo meshsave.txt foi deletado com sucesso!" : "Arquivo meshsave.txt não foi encontrado.", 
                "", success ? "#00ff00" : "#ffaa00", success ? "SUCESSO" : "AVISO");
        }

        private void OnCreateRestorePointClick(SettingsTextBox restorePointInput)
        {
            string gameFolder = GetGameSaveFolder();
            string restorePointName = restorePointInput != null ? restorePointInput.GetValue() : "";
            string createdPointName = backupManager.CreateRestorePoint(gameFolder, restorePointName);
            if (!string.IsNullOrEmpty(createdPointName))
                ShowPopup("Ponto de restauração foi criado com sucesso!", createdPointName, "#ff9900", "SUCESSO");
            else
                ShowPopup("Falha ao criar ponto de restauração!\n\nNenhum arquivo de save válido foi encontrado.", "", "#ff0000", "FALHA");
        }

        private void OnRestartLevel1Click()
        {
            ModConsole.Log("[BackupSave] Reiniciando o menu");
            UnityEngine.Application.LoadLevel(1);
        }

        private void OnShowModInfoClick()
        {
            string infoText = "<b>BackupSave - Sistema de Backup</b>\n\n<b>Recursos:</b>\n";
            infoText += "• <b>Backups automáticos:</b> Toda vez que carrega o jogo um backup é criado\n";
            infoText += "• <b>Restauração automática:</b> Detecta morte e restaura o último backup automaticamente\n";
            infoText += "• <b>Pontos de Restauração:</b> Crie pontos permanentes para restaurar sempre que quiser\n";
            infoText += "• <b>Controle de Limite:</b> Defina quantos backups manter\n";
            infoText += "• <b>Prefixo do Personagem:</b> Adicione o nome do personagem ao nome do backup\n";
            infoText += "• <b>Deletar meshsave:</b> Redefina o formato do veículo quando necessário\n";
            if (ModLoader.CurrentGame == Game.MySummerCar) infoText += "• <b>Importar saves do SaveBackuper:</b> Importe seus saves do MOD SaveBackuper\n";
            if (ModLoader.CurrentGame == Game.MyWinterCar) infoText += "• <b>Importar Save:</b> Importe seu save do My Summer Car para o My Winter Car\n";
            infoText += "\n<b>Modos de Restauração:</b>\n";
            infoText += "• <b>Desligado:</b> Não restaura automaticamente\n";
            infoText += "• <b>Restaurar Tudo:</b> Restaura todos os arquivos do save\n";
            infoText += "• <b>Restaurar Mantendo as Lápides:</b> Restaura mas mantém as lápides";
            ModUI.ShowMessage(infoText, "INFORMAÇÕES DO MOD");
        }

        private void OnShowCreditsClick()
        {
            string creditsText = "<b>BackupSave - Créditos</b>\n\n";
            creditsText += "<b>Baseado em:</b>\n";
            creditsText += "SaveBackuper por AnimeForevere";
            ModUI.ShowMessage(creditsText, "CRÉDITOS");
        }

        private void OnImportSaveClick()
        {
            string customBackupName = "MWC-Backup";
            int backupLimit = GetBackupLimit();
            
            saveImportManager.ImportSaveFromMSCToMWC(backupManager, customBackupName, backupLimit);
        }



        private void OnImportAllExternalBackupsClick()
        {
            int backupLimit = GetBackupLimit();
            saveImportManager.ImportAllExternalBackups(backupLimit);
        }

        private void ShowPopup(string message, string detailName = "", string color = "#00ff00", string title = "SUCESSO")
        {
            string detail = string.IsNullOrEmpty(detailName) ? "" : "\n<color=" + color + ">" + detailName + "</color>";
            ModUI.ShowMessage(message + detail, title);
        }

        private void Mod_OnLoad()
        {
            string gameFolder = GetGameSaveFolder();
            autoRestoreManager.SetAutoRestoreMode(GetAutoRestoreMode());
            if (GetPrefixCharacterName())
            {
                string characterName = backupManager.GetCharacterFirstName(gameFolder);
                if (!string.IsNullOrEmpty(characterName))
                {
                    autoRestoreManager.SetCharacterFirstName(characterName);
                    ModConsole.Log("<color=#00ff00>" + LOG_PREFIX + "Prefixo do personagem ativado: " + characterName + "</color>");
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
                ModConsole.Log("<color=" + logColor + ">" + LOG_PREFIX + "Restauração Automática: " + MODE_NAMES[currentMode] + "</color>");
            }
            lastAutoRestoreMode = currentMode;
            autoRestoreManager.SetAutoRestoreMode(currentMode);
            autoRestoreManager.MonitorPlayerDeath(gameFolder);
            if (GetAutoDeleteMeshsave() && UnityEngine.Application.loadedLevel == 1)
                backupManager.DeleteMeshSaveFile(gameFolder);
        }

        private void OnMenuLoad()
        {
            string gameFolder = GetGameSaveFolder();
            autoRestoreManager.CompleteAutoRestore(gameFolder);
        }
    }
}
