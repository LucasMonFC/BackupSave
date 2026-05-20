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
                        File.Copy(sourceFile, Path.Combine(destPath, fileName), true);
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
                    string errorMsg = LocalizationManager.Text("Error: No save found in My Summer Car!", "Erro: Nenhum save encontrado em My Summer Car!");
                    if (localizationManager != null)
                    {
                        errorMsg = localizationManager.GetString("popup", "importSaveNotFound", errorMsg);
                    }
                    string errorTitle = LocalizationManager.Text("FAILURE", "FALHA");
                    if (localizationManager != null)
                    {
                        errorTitle = localizationManager.GetString("popup", "titleFailure", errorTitle);
                    }
                    ModUI.ShowMessage(errorMsg, errorTitle);
                    return;
                }

                bool mwcHasValidSave = backupManager.ValidateSaveFiles("My Winter Car");
                bool backupCreated = false;
                
                if (mwcHasValidSave)
                {
                    string creatingMsg = LocalizationManager.Text("[BackupSave] Creating backup of current My Winter Car save...", "[BackupSave] Criando backup do save atual do My Winter Car...");
                    if (localizationManager != null)
                    {
                        creatingMsg = localizationManager.GetString("log", "creatingMWCBackup", creatingMsg);
                    }
                    ModConsole.Log("<color=#ffffff>" + creatingMsg + "</color>");
                    backupCreated = backupManager.DoBackup("My Winter Car", customBackupName, backupLimit);
                }

                if (!Directory.Exists(mwcSavePath))
                    Directory.CreateDirectory(mwcSavePath);

                ManageSaveFiles(mscSavePath, mwcSavePath, true);  // Delete
                ManageSaveFiles(mscSavePath, mwcSavePath);        // Copy

                string importedMsg = LocalizationManager.Text("[BackupSave] Save successfully imported from My Summer Car to My Winter Car!", "[BackupSave] Save importado com sucesso do My Summer Car para My Winter Car!");
                if (localizationManager != null)
                {
                    importedMsg = localizationManager.GetString("log", "saveImported", importedMsg);
                }
                ModConsole.Log("<color=#00ff00>" + importedMsg + "</color>");
                
                string message = "";
                
                if (mwcHasValidSave)
                {
                    message = LocalizationManager.Text("Backup created successfully!\nSave from My Summer Car was imported to My Winter Car.", "Backup criado com sucesso!\nSave do My Summer Car foi importado para My Winter Car.");
                    if (localizationManager != null)
                    {
                        message = localizationManager.GetString("popup", "successImport", message);
                    }
                    
                    if (!backupCreated)
                    {
                        string warning = LocalizationManager.Text("WARNING: Failed to create backup of previous save.", "AVISO: Falha ao criar backup do save anterior.");
                        if (localizationManager != null)
                        {
                            warning = localizationManager.GetString("popup", "warningBackupFailed", warning);
                        }
                        message += "\n\n" + warning;
                    }
                }
                else
                {
                    message = LocalizationManager.Text("Save from My Summer Car was imported to My Winter Car!\nWarning: No previous save to backup.", "Save do My Summer Car foi importado para My Winter Car!\nAviso: Não havia save anterior para backup.");
                    if (localizationManager != null)
                    {
                        message = localizationManager.GetString("popup", "successImportNoSave", message);
                    }
                }
                
                string title = LocalizationManager.Text("SUCCESS", "SUCESSO");
                if (localizationManager != null)
                {
                    title = localizationManager.GetString("popup", "titleSuccess", title);
                }
                ModUI.ShowMessage(backupCreated ? message : message, title);
            }
            catch (Exception ex)
            {
                string errorPrefix = LocalizationManager.Text("Error importing save: ", "Erro ao importar save: ");
                if (localizationManager != null)
                {
                    errorPrefix = localizationManager.GetString("popup", "errorImport", errorPrefix);
                }
                string titleFailure = LocalizationManager.Text("FAILURE", "FALHA");
                if (localizationManager != null)
                {
                    titleFailure = localizationManager.GetString("popup", "titleFailure", titleFailure);
                }
                ModUI.ShowMessage(errorPrefix + ex.Message, titleFailure);
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
                string alreadyMsg = LocalizationManager.Text("Import has already been completed.\nThe backup list is already updated.", "A importação já foi concluída.\nA lista de backups já está atualizada.");
                string titleAlreadyDone = LocalizationManager.Text("IMPORT ALREADY COMPLETED", "IMPORTAÇÃO JÁ REALIZADA");
                if (localizationManager != null)
                {
                    alreadyMsg = localizationManager.GetString("popup", "importAlreadyDone", alreadyMsg);
                    titleAlreadyDone = localizationManager.GetString("popup", "titleImportAlreadyDone", titleAlreadyDone);
                }
                ModUI.ShowMessage(alreadyMsg, titleAlreadyDone);
                return 0;
            }
            
            string[] backups = GetExternalBackupList();
            if (backups.Length == 0)
            {
                string alreadyMsg = LocalizationManager.Text("Import has already been completed.\nThe backup list is already updated.", "A importação já foi concluída.\nA lista de backups já está atualizada.");
                string titleAlreadyDone = LocalizationManager.Text("IMPORT ALREADY COMPLETED", "IMPORTAÇÃO JÁ REALIZADA");
                if (localizationManager != null)
                {
                    alreadyMsg = localizationManager.GetString("popup", "importAlreadyDone", alreadyMsg);
                    titleAlreadyDone = localizationManager.GetString("popup", "titleImportAlreadyDone", titleAlreadyDone);
                }
                ModUI.ShowMessage(alreadyMsg, titleAlreadyDone);
                return 0;
            }
            
            int importedCount = 0;
            bool allImported = true;
            foreach (string backupName in backups)
            {
                string externalBackupPath = Path.Combine(externalBackupRootPath, backupName);
                string importedPrefix = LocalizationManager.Text("IMPORTED - ", "IMPORTADO - ");
                if (localizationManager != null)
                {
                    importedPrefix = localizationManager.GetImportedPrefix();
                }
                string importedName = importedPrefix + backupName;
                
                if (backupManager.ImportExternalBackupPath(externalBackupPath, importedName, backupLimit, false))
                {
                    importedCount++;
                    string importedMsg = LocalizationManager.Text("[BackupSave] External backup imported: ", "[BackupSave] Backup externo importado: ");
                    if (localizationManager != null)
                    {
                        importedMsg = localizationManager.GetString("log", "externalBackupImported", importedMsg);
                    }
                    ModConsole.Log("<color=#00ff00>" + importedMsg + importedName + "</color>");
                }
                else
                {
                    allImported = false;
                    string errorMsg = LocalizationManager.Text("[BackupSave] Error creating backup", "[BackupSave] Erro ao criar backup");
                    if (localizationManager != null)
                    {
                        errorMsg = localizationManager.GetString("log", "errorCreatingBackup", errorMsg);
                    }
                    ModConsole.Error(errorMsg + ": " + importedName);
                }
            }
            
            // Aplicar limite de backups após importar todos
            backupManager.ManageBackupLimit("My Summer Car", backupLimit);
            
            // Deletar pasta Backup externa após importação bem-sucedida
            if (importedCount > 0 && allImported)
            {
                Directory.Delete(externalBackupRootPath, true);
                string folderDeletedMsg = LocalizationManager.Text("[BackupSave] External backup folder successfully deleted.", "[BackupSave] Pasta de backup externo deletada com sucesso.");
                if (localizationManager != null)
                {
                    folderDeletedMsg = localizationManager.GetString("log", "externalBackupFolderDeleted", folderDeletedMsg);
                }
                ModConsole.Log("<color=#00ff00>" + folderDeletedMsg + "</color>");
                
                // Pop-up informando importação concluída
                string completedStart = LocalizationManager.Text("Total of ", "Total de ");
                string completedEnd = LocalizationManager.Text(" backup(s) imported successfully!\nBackup folder deleted.\nThe backup list has been updated.", " backup(s) importado(s) com sucesso!\nPasta de backup excluída.\nA lista de backups foi atualizada.");
                string titleCompleted = LocalizationManager.Text("IMPORT COMPLETED", "IMPORTAÇÃO CONCLUÍDA");
                if (localizationManager != null)
                {
                    completedStart = localizationManager.GetString("popup", "importCompleted", completedStart);
                    completedEnd = localizationManager.GetString("popup", "importCompletedEnd", completedEnd);
                    titleCompleted = localizationManager.GetString("popup", "titleImportCompleted", titleCompleted);
                }
                string completedMsg = completedStart + importedCount + completedEnd;
                ModUI.ShowMessage(completedMsg, titleCompleted);
            }
            else if (importedCount > 0)
            {
                string partialMsg = LocalizationManager.Text("Some backups were imported, but a few failed.\nThe original folder was kept so you can try again.", "Alguns backups foram importados, mas houve falhas.\nA pasta original foi mantida para você tentar novamente.");
                string titleWarning = LocalizationManager.Text("WARNING", "AVISO");
                if (localizationManager != null)
                {
                    partialMsg = localizationManager.GetString("popup", "importPartial", partialMsg);
                    titleWarning = localizationManager.GetString("popup", "titleWarning", titleWarning);
                }
                ModUI.ShowMessage(partialMsg, titleWarning);
            }
            else if (!allImported)
            {
                string failMsg = LocalizationManager.Text("Failed to import backups.\nThe original folder was kept so you can try again.", "Falha ao importar os backups.\nA pasta original foi mantida para você tentar novamente.");
                string titleFailure = LocalizationManager.Text("FAILURE", "FALHA");
                if (localizationManager != null)
                {
                    failMsg = localizationManager.GetString("popup", "importFailed", failMsg);
                    titleFailure = localizationManager.GetString("popup", "titleFailure", titleFailure);
                }
                ModUI.ShowMessage(failMsg, titleFailure);
            }
            
            return importedCount;
        }
    }
}
