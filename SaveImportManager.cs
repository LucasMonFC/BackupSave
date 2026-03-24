using MSCLoader;
using System;
using System.Collections.Generic;
using System.IO;

namespace BackupSave
{
    public class SaveImportManager
    {
        private string mscSavesPath;
        private BackupManager backupManager;
        private LocalizationManager localizationManager;
        
        private readonly string[] filesToImport = { "defaultES2File.txt", "graveyard.txt", "items.txt", "notepad.txt", "options.txt", "trophies.txt" };

        public SaveImportManager(string mscSavesPath, BackupManager backupManager = null, LocalizationManager localizationManager = null)
        {
            this.mscSavesPath = mscSavesPath;
            this.backupManager = backupManager;
            this.localizationManager = localizationManager;
        }

        /// <summary>
        /// Check if the key save file exists (defaultES2File.txt)
        /// </summary>
        private bool HasKeyFile(string mscSavePath)
        {
            string keyFile = Path.Combine(mscSavePath, "defaultES2File.txt");
            return File.Exists(keyFile);
        }

        /// <summary>
        /// Gerencia arquivos de save (deleta ou copia conforme necessário)
        /// </summary>
        private void ManageSaveFiles(string sourcePath, string destPath, bool isDelete = false)
        {
            if (isDelete)
            {
                if (Directory.Exists(destPath))
                {
                    foreach (FileInfo file in new DirectoryInfo(destPath).GetFiles())
                        file.Delete();
                }
            }
            else
            {
                foreach (string fileName in filesToImport)
                {
                    string sourceFile = Path.Combine(sourcePath, fileName);
                    if (File.Exists(sourceFile))
                        File.Copy(sourceFile, Path.Combine(destPath, fileName));
                }
            }
        }

        /// <summary>
        /// Import save from My Summer Car to My Winter Car
        /// Checks if the key file (defaultES2File.txt) exists, then imports all available files
        /// </summary>
        public void ImportSaveFromMSCToMWC(BackupManager backupManager, string customBackupName = "", int backupLimit = 50)
        {
            try
            {
                string mscSavePath = Path.Combine(mscSavesPath, "My Summer Car");
                string mwcSavePath = Path.Combine(mscSavesPath, "My Winter Car");

                if (!HasKeyFile(mscSavePath))
                {
                    string errorMsg = localizationManager != null 
                        ? localizationManager.GetString("popup", "importSaveNotFound", "Error: No save found in My Summer Car!")
                        : "Erro: Nenhum save encontrado no My Summer Car!";
                    string errorTitle = localizationManager != null
                        ? localizationManager.GetString("popup", "titleFailure", "FALHA")
                        : "FALHA";
                    ModUI.ShowMessage(errorMsg, errorTitle);
                    return;
                }

                bool mwcHasValidSave = backupManager.ValidateSaveFiles("My Winter Car");
                bool backupCreated = false;
                
                if (mwcHasValidSave)
                {
                    string creatingMsg = localizationManager != null
                        ? localizationManager.GetString("log", "creatingMWCBackup", "[BackupSave] Creating backup of current My Winter Car save...")
                        : "[BackupSave] Criando backup do save atual do My Winter Car...";
                    ModConsole.Log("<color=#ffffff>" + creatingMsg + "</color>");
                    backupCreated = backupManager.DoBackup("My Winter Car", customBackupName, backupLimit);
                }

                if (!Directory.Exists(mwcSavePath))
                    Directory.CreateDirectory(mwcSavePath);

                ManageSaveFiles(mscSavePath, mwcSavePath, true);  // Delete
                ManageSaveFiles(mscSavePath, mwcSavePath);        // Copy

                string importedMsg = localizationManager != null
                    ? localizationManager.GetString("log", "saveImported", "[BackupSave] Save importado com sucesso do My Summer Car para My Winter Car!")
                    : "[BackupSave] Save importado com sucesso do My Summer Car para My Winter Car!";
                ModConsole.Log("<color=#00ff00>" + importedMsg + "</color>");
                
                string message = "";
                
                if (mwcHasValidSave)
                {
                    message = localizationManager != null
                        ? localizationManager.GetString("popup", "successImport", "Backup criado com sucesso!\nSave do My Summer Car foi importado para My Winter Car.")
                        : "Backup criado com sucesso!\nSave do My Summer Car foi importado para My Winter Car.";
                    
                    if (!backupCreated)
                        message += "\n\n" + localizationManager.GetString("popup", "warningBackupFailed", "AVISO: Falha ao criar backup do save anterior.");
                }
                else
                {
                    message = localizationManager != null
                        ? localizationManager.GetString("popup", "successImportNoSave", "Save do My Summer Car foi importado para My Winter Car!\nAviso: Não havia save anterior para backup.")
                        : "Save do My Summer Car foi importado para My Winter Car!\nAviso: Não havia save anterior para backup.";
                }
                
                string title = localizationManager != null
                    ? localizationManager.GetString("popup", "titleSuccess", "SUCESSO")
                    : "SUCESSO";
                ModUI.ShowMessage(backupCreated ? message : message, title);
            }
            catch (Exception ex)
            {
                string errorPrefix = localizationManager != null
                    ? localizationManager.GetString("popup", "errorImport", "Error importing save: ")
                    : "Erro ao importar save: ";
                ModUI.ShowMessage(errorPrefix + ex.Message, localizationManager != null ? localizationManager.GetString("popup", "titleFailure", "FALHA") : "FALHA");
            }
        }

        /// <summary>
        /// Check if MSC save exists (checks for the key file)
        /// </summary>
        public bool HasMSCSave() => HasKeyFile(Path.Combine(mscSavesPath, "My Summer Car"));

        /// <summary>
        /// IMPORTAÇÃO DE BACKUPS DO SAVEBACKUPER
        /// Verifica se existem backups externos do SaveBackuper
        /// </summary>
        public bool HasExternalBackups()
        {
            try
            {
                string externalBackupPath = GetExternalBackupRootPath();
                
                if (!Directory.Exists(externalBackupPath))
                    return false;
                
                DirectoryInfo[] backups = new DirectoryInfo(externalBackupPath).GetDirectories();
                return backups.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtém o caminho raiz dos backups externos do SaveBackuper
        /// </summary>
        private string GetExternalBackupRootPath()
        {
            return Path.Combine(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                        .Replace("Roaming", "LocalLow"), 
                    "Amistech"), 
                "Backup");
        }

        /// <summary>
        /// Lista todos os backups externos do SaveBackuper
        /// </summary>
        private string[] GetExternalBackupList()
        {
            try
            {
                string externalBackupPath = GetExternalBackupRootPath();
                
                if (!Directory.Exists(externalBackupPath))
                    return new string[] { };
                
                DirectoryInfo[] backups = new DirectoryInfo(externalBackupPath).GetDirectories();
                if (backups.Length == 0)
                    return new string[] { };
                
                System.Array.Sort(backups, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                List<string> names = new List<string>();
                
                foreach (var dir in backups)
                    names.Add(dir.Name);
                
                return names.ToArray();
            }
            catch
            {
                return new string[] { };
            }
        }

        /// <summary>
        /// Importa todos os backups externos do SaveBackuper para o BackupSave
        /// </summary>
        public int ImportAllExternalBackups(int backupLimit)
        {
            if (backupManager == null)
                return 0;

            string externalBackupRootPath = GetExternalBackupRootPath();
            
            if (!Directory.Exists(externalBackupRootPath))
            {
                string alreadyMsg = localizationManager != null ? localizationManager.GetString("popup", "importAlreadyDone", "A importação já foi concluída.\nFeche e abra o jogo para atualizar a lista de backups.") : "A importação já foi concluída.\nFeche e abra o jogo para atualizar a lista de backups.";
                string titleAlreadyDone = localizationManager != null ? localizationManager.GetString("popup", "titleImportAlreadyDone", "IMPORTAÇÃO JÁ REALIZADA") : "IMPORTAÇÃO JÁ REALIZADA";
                ModUI.ShowMessage(alreadyMsg, titleAlreadyDone);
                return 0;
            }
            
            string[] backups = GetExternalBackupList();
            if (backups.Length == 0)
            {
                string alreadyMsg = localizationManager != null ? localizationManager.GetString("popup", "importAlreadyDone", "A importação já foi concluída.\nFeche e abra o jogo para atualizar a lista de backups.") : "A importação já foi concluída.\nFeche e abra o jogo para atualizar a lista de backups.";
                string titleAlreadyDone = localizationManager != null ? localizationManager.GetString("popup", "titleImportAlreadyDone", "IMPORTAÇÃO JÁ REALIZADA") : "IMPORTAÇÃO JÁ REALIZADA";
                ModUI.ShowMessage(alreadyMsg, titleAlreadyDone);
                return 0;
            }
            
            int importedCount = 0;
            foreach (string backupName in backups)
            {
                string externalBackupPath = Path.Combine(externalBackupRootPath, backupName);
                string importedPrefix = localizationManager != null ? localizationManager.GetImportedPrefix() : "IMPORTADO - ";
                string importedName = importedPrefix + backupName;
                
                // Usar BackupManager para criar o backup importado
                backupManager.ImportExternalBackupPath(externalBackupPath, importedName, backupLimit);
                importedCount++;
                
                string importedMsg = localizationManager != null ? localizationManager.GetString("log", "externalBackupImported", "[BackupSave] Backup externo importado: ") : "[BackupSave] Backup externo importado: ";
                ModConsole.Log("<color=#00ff00>" + importedMsg + importedName + "</color>");
            }
            
            // Aplicar limite de backups após importar todos
            backupManager.ManageBackupLimit("My Summer Car", backupLimit);
            
            // Deletar pasta Backup externa após importação bem-sucedida
            if (importedCount > 0)
            {
                Directory.Delete(externalBackupRootPath, true);
                string folderDeletedMsg = localizationManager != null ? localizationManager.GetString("log", "externalBackupFolderDeleted", "[BackupSave] Pasta de backup externo deletada com sucesso.") : "[BackupSave] Pasta de backup externo deletada com sucesso.";
                ModConsole.Log("<color=#00ff00>" + folderDeletedMsg + "</color>");
                
                // Pop-up informando importação concluída
                string completedMsg = (localizationManager != null ? localizationManager.GetString("popup", "importCompleted", "Total de ") : "Total de ") + importedCount + (localizationManager != null ? localizationManager.GetString("popup", "importCompletedEnd", " backup(s) importado(s) com sucesso!\nPasta de backup excluída.\nFeche e abra o jogo para que os backups apareçam na lista.") : " backup(s) importado(s) com sucesso!\nPasta de backup excluída.\nFeche e abra o jogo para que os backups apareçam na lista.");
                string titleCompleted = localizationManager != null ? localizationManager.GetString("popup", "titleImportCompleted", "IMPORTAÇÃO CONCLUÍDA") : "IMPORTAÇÃO CONCLUÍDA";
                ModUI.ShowMessage(completedMsg, titleCompleted);
            }
            
            return importedCount;
        }
    }
}
