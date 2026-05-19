using MSCLoader;
using System;
using System.Collections.Generic;
using System.IO;

namespace BackupSave
{
    public class AutoRestoreManager
    {
        private BackupManager backupManager;
        private string mscSavesPath;
        private LocalizationManager localizationManager;
        private bool lastSaveFilesExisted = true;
        private bool flagCreatedThisSession = false;
        private bool backupWasCreated = false;
        private readonly string flagFilePath;
        private int autoRestoreMode = 1; // 0 = desligado, 1 = restaurar tudo, 2 = restaurar mantendo as lápides
        private string characterFirstName = ""; // Nome do personagem para prefixar backups
        
        // OTIMIZAÇÃO: Throttle verificar morte apenas 1x por segundo
        private float lastMonitorCheckTime = 0f;
        private const float MONITOR_CHECK_INTERVAL = 1.0f; // Verificar a cada 1 segundo
        
        // OTIMIZAÇÃO: Cache de caminho save (evita Path.Combine repeaters)
        // Máximo 2 entradas (MSC e MWC) - limpar se exceder
        private Dictionary<string, string> savePathCache = new Dictionary<string, string>();

        public AutoRestoreManager(BackupManager backupManager, string mscSavesPath, LocalizationManager localizationManager = null)
        {
            this.backupManager = backupManager;
            this.mscSavesPath = mscSavesPath;
            this.localizationManager = localizationManager;
            this.flagFilePath = Path.Combine(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                        .Replace("Roaming", "LocalLow"), 
                    "Amistech"), 
                "auto_restore.flag");
        }

        /// <summary>
        /// Define o nome do primeiro personagem para prefixar os backups
        /// </summary>
        public void SetCharacterFirstName(string firstName)
        {
            characterFirstName = firstName ?? "";
        }

        /// <summary>
        /// Configura o modo de restauração automática
        /// 0 = Desligado, 1 = Restaurar Tudo, 2 = Restaurar mantendo as lápides
        /// </summary>
        public void SetAutoRestoreMode(int mode)
        {
            autoRestoreMode = mode;
        }

        /// <summary>
        /// ETAPA 1: Inicializa o backup automático e prepara o monitoramento
        /// Chamado quando o jogo carrega (Mod_OnLoad)
        /// </summary>
        public void InitializeAutoRestore(string gameFolder, string customBackupName, int backupLimit)
        {
            try
            {
                string backupPath = backupManager.GetBackupPath(gameFolder);
                if (!Directory.Exists(backupPath))
                    Directory.CreateDirectory(backupPath);

                if (backupManager.DoBackup(gameFolder, customBackupName, backupLimit, characterFirstName))
                {
                    // Definir estado real dos saves para monitoramento
                    string savePath = Path.Combine(mscSavesPath, gameFolder);
                    lastSaveFilesExisted = CheckSaveFilesExist(gameFolder, savePath);
                    backupWasCreated = true;
                }
                else
                {
                    backupWasCreated = false;
                }

                // Mostrar status da Restauração Automática quando save é carregado
                string[] modeLogMessages = new string[] 
                {
                    localizationManager != null ? localizationManager.GetString("log", "autoRestoreModeDisabled", "[BackupSave] Restauração Automática: Desligada") : "[BackupSave] Restauração Automática: Desligada",
                    localizationManager != null ? localizationManager.GetString("log", "autoRestoreModeRestoreAll", "[BackupSave] Restauração Automática: Restaurar Tudo") : "[BackupSave] Restauração Automática: Restaurar Tudo",
                    localizationManager != null ? localizationManager.GetString("log", "autoRestoreModeRestoreGraveyard", "[BackupSave] Restauração Automática: Restaurar mantendo as lápides") : "[BackupSave] Restauração Automática: Restaurar mantendo as lápides"
                };
                string logColor = autoRestoreMode == 0 ? "#ff0000" : "#00ff00"; // Vermelho se desligada, verde se ligada
                ModConsole.Log("<color=" + logColor + ">" + modeLogMessages[autoRestoreMode] + "</color>");
            }
            catch (Exception ex)
            {
                ModConsole.Error("[BackupSave] Erro ao fazer backup do save\n" + ex.Message);
                backupWasCreated = false;
            }
        }

        /// <summary>
        /// ETAPA 2: Monitora a morte do jogador (deletação do arquivo de save)
        /// Chamado a cada frame em ModUpdate
        /// OTIMIZAÇÃO: Throttle a verificação para apenas 1x por segundo
        /// </summary>
        public void MonitorPlayerDeath(string gameFolder)
        {
            try
            {
                // OTIMIZAÇÃO: Early returns para evitar cálculos desnecessários
                if (autoRestoreMode == 0 || flagCreatedThisSession || !backupWasCreated)
                    return;

                // OTIMIZAÇÃO: Throttle - verificar apenas a cada 1 segundo
                lastMonitorCheckTime += UnityEngine.Time.deltaTime;
                if (lastMonitorCheckTime < MONITOR_CHECK_INTERVAL)
                    return;

                lastMonitorCheckTime = 0f;

                // OTIMIZAÇÃO: Cache de savePath
                string savePath = GetCachedSavePath(gameFolder);

                // Verificar se arquivos de save existem
                bool saveFilesExist = CheckSaveFilesExist(gameFolder, savePath);

                // ETAPA 2: Detectar MUDANÇA - arquivos FORAM DELETADOS (tinham e agora não têm)
                if (lastSaveFilesExisted && !saveFilesExist)
                {
                    // Modo 2 = preservar graveyard.txt
                    bool preserveGraveyard = (autoRestoreMode == 2);
                    backupManager.RestoreLatestBackupAuto(gameFolder, preserveGraveyard);

                    // Obter nome do backup restaurado
                    string backupName = GetLatestBackupName(gameFolder);

                    // Criar flag para confirmar no menu, salvando modo e nome do backup
                    try
                    {
                        string flagContent = "pending:" + autoRestoreMode + ":" + backupName;
                        File.WriteAllText(flagFilePath, flagContent);
                        flagCreatedThisSession = true;
                    }
                    catch { }

                    return;
                }

                // Atualizar estado SOMENTE se não criou flag
                lastSaveFilesExisted = saveFilesExist;
            }
            catch (Exception ex)
            {
                ModConsole.Error("[BackupSave] Erro ao verificar morte: " + ex.Message);
            }
        }

        /// <summary>
        /// ETAPA 3: Completa a restauração automática ao voltar ao menu
        /// Chamado quando o menu carrega (OnMenuLoad)
        /// </summary>
        public void CompleteAutoRestore(string gameFolder)
        {
            try
            {
                if (UnityEngine.Application.loadedLevel == 1)  // Menu principal
                {
                    // ETAPA 3: Ler flag e fazer segunda restauração
                    if (File.Exists(flagFilePath))
                    {
                        // Extrair modo, preservação e nome do backup
                        int mode = 0;
                        bool preserveGraveyard = false;
                        string backupName = "";
                        ParseAutoRestoreFlag(out mode, out preserveGraveyard, out backupName);

                        if (backupManager.RestoreLatestBackupAuto(gameFolder, preserveGraveyard))
                        {
                            // Deletar flag
                            File.Delete(flagFilePath);
                            
                            // Mensagem customizada baseada no modo com nome do backup em cor verde
                            string message = localizationManager != null ? localizationManager.GetString("popup", "deathMessage", "Você morreu!") : "Você morreu!";
                            message += "\n" + (localizationManager != null ? localizationManager.GetString("popup", "successRestored", "Seu backup foi restaurado com sucesso.") : "Seu backup foi restaurado com sucesso.");
                            if (!string.IsNullOrEmpty(backupName))
                            {
                                message += "\n<color=#00ff00>" + backupName + "</color>";
                            }
                            if (preserveGraveyard)
                                message += "\n" + (localizationManager != null ? localizationManager.GetString("popup", "graveyardPreserved", "Seu histórico de morte e lápides foram preservados.") : "Seu histórico de morte e lápides foram preservados.");
                                
                            string titleSuccess = localizationManager != null ? localizationManager.GetString("popup", "titleSuccess", "SUCESSO") : "SUCESSO";
                            ModUI.ShowMessage(message, titleSuccess);

                            // Reset para próximo ciclo
                            flagCreatedThisSession = false;
                            backupWasCreated = false;
                        }
                        else
                        {
                            string failMsg = localizationManager != null ? localizationManager.GetString("popup", "failRestore", "Falha ao restaurar seu backup!\nVerifique se você tem um backup disponível.") : "Falha ao restaurar seu backup!\nVerifique se você tem um backup disponível.";
                            string titleFailure = localizationManager != null ? localizationManager.GetString("popup", "titleFailure", "FALHA") : "FALHA";
                            ModUI.ShowMessage(failMsg, titleFailure);
                            try
                            {
                                File.Delete(flagFilePath);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModConsole.Error("[BackupSave] Erro ao processar menu: " + ex.Message);
            }
        }

        /// <summary>
        /// Verifica se os arquivos de save existem para o jogo especificado
        /// </summary>
        private bool CheckSaveFilesExist(string gameFolder, string savePath)
        {
            if (gameFolder == "My Summer Car")
            {
                return File.Exists(Path.Combine(savePath, "defaultES2File.txt"));
            }
            else // My Winter Car
            {
                return File.Exists(Path.Combine(savePath, "carparts.txt")) &&
                       File.Exists(Path.Combine(savePath, "savefile.txt"));
            }
        }

        /// <summary>
        /// Parse auto restore flag file to extract mode, preservation settings, and backup name
        /// Formato: "pending:0:BackupName", "pending:1:BackupName", "pending:2:BackupName"
        /// </summary>
        private void ParseAutoRestoreFlag(out int mode, out bool preserveGraveyard, out string backupName)
        {
            mode = 0;
            preserveGraveyard = false;
            backupName = "";

            try
            {
                if (File.Exists(flagFilePath))
                {
                    string flagContent = File.ReadAllText(flagFilePath);
                    if (flagContent.Contains(":"))
                    {
                        string[] parts = flagContent.Split(':');
                        if (parts.Length >= 2)
                        {
                            int parsedMode;
                            if (int.TryParse(parts[1], out parsedMode))
                            {
                                mode = parsedMode;
                                preserveGraveyard = (mode == 2);
                            }
                        }
                        if (parts.Length >= 3)
                        {
                            backupName = parts[2];
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Obtém o nome do backup mais recente para o jogo especificado
        /// </summary>
        private string GetLatestBackupName(string gameFolder)
        {
            return backupManager != null ? backupManager.GetLatestBackupName(gameFolder) : "";
        }

        /// <summary>
        /// OTIMIZAÇÃO: Cache de savePath para evitar Path.Combine repetido
        /// Limita a 2 entradas (MSC e MWC) para evitar memory leak
        /// </summary>
        private string GetCachedSavePath(string gameFolder)
        {
            string cachedPath;
            if (savePathCache.TryGetValue(gameFolder, out cachedPath))
                return cachedPath;
            
            // Se cache está cheio, limpar antes de adicionar nova entrada
            if (savePathCache.Count >= 2)
                savePathCache.Clear();

            cachedPath = Path.Combine(mscSavesPath, gameFolder);
            savePathCache[gameFolder] = cachedPath;
            return cachedPath;
        }

    }
}
