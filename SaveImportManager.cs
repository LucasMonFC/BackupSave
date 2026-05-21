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
                        errorMsg = localizationManager.GetString("message", "importSaveNotFound", errorMsg);
                    }
                    ModConsole.Error(errorMsg);
                    return;
                }

                bool mwcHasValidSave = backupManager.ValidateSaveFiles("My Winter Car");
                bool backupCreated = false;
                
                if (mwcHasValidSave)
                {
                    backupCreated = backupManager.DoBackup("My Winter Car", customBackupName, backupLimit);
                }

                if (!Directory.Exists(mwcSavePath))
                    Directory.CreateDirectory(mwcSavePath);

                ManageSaveFiles(mscSavePath, mwcSavePath, true);  // Delete
                ManageSaveFiles(mscSavePath, mwcSavePath);        // Copy
                
                string message = "";
                
                if (mwcHasValidSave)
                {
                    message = LocalizationManager.Text("[BackupSave] Backup created successfully.\nSave from My Summer Car was imported to My Winter Car.", "[BackupSave] Backup criado com sucesso.\nSave do My Summer Car foi importado para My Winter Car.");
                    if (localizationManager != null)
                    {
                        message = localizationManager.GetString("message", "successImport", message);
                    }
                    
                    if (!backupCreated)
                    {
                        string warning = LocalizationManager.Text("WARNING: Failed to create backup of previous save.", "AVISO: Falha ao criar backup do save anterior.");
                        if (localizationManager != null)
                        {
                            warning = localizationManager.GetString("message", "warningBackupFailed", warning);
                        }
                        message += "\n\n" + warning;
                    }
                }
                else
                {
                    message = LocalizationManager.Text("[BackupSave] Save from My Summer Car was imported to My Winter Car.\nWarning: No previous save to backup.", "[BackupSave] Save do My Summer Car foi importado para My Winter Car.\nAviso: Não havia save anterior para backup.");
                    if (localizationManager != null)
                    {
                        message = localizationManager.GetString("message", "successImportNoSave", message);
                    }
                }
                
                ModConsole.Log("<color=#00ff00>" + message + "</color>");
            }
            catch (Exception ex)
            {
                string errorPrefix = LocalizationManager.Text("Error importing save: ", "Erro ao importar save: ");
                if (localizationManager != null)
                {
                    errorPrefix = localizationManager.GetString("message", "errorImport", errorPrefix);
                }
                ModConsole.Error(errorPrefix + ex.Message);
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
                return GetExternalBackupPaths().Length > 0;
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
            if (!string.IsNullOrEmpty(mscSavesPath))
                return Path.Combine(mscSavesPath, "Backup");

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
        private string[] GetExternalBackupPaths()
        {
            try
            {
                string externalBackupPath = GetExternalBackupRootPath();
                
                if (!Directory.Exists(externalBackupPath))
                    return new string[] { };

                DirectoryInfo root = new DirectoryInfo(externalBackupPath);
                List<FileSystemInfo> backups = new List<FileSystemInfo>();

                foreach (DirectoryInfo dir in root.GetDirectories())
                {
                    if (IsImportableExternalBackupDirectory(dir))
                        backups.Add(dir);
                }

                foreach (FileInfo file in root.GetFiles("*.zip"))
                    backups.Add(file);

                if (backups.Count == 0)
                    return new string[] { };
                
                backups.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                List<string> paths = new List<string>();
                
                foreach (FileSystemInfo item in backups)
                    paths.Add(item.FullName);
                
                return paths.ToArray();
            }
            catch
            {
                return new string[] { };
            }
        }

        private bool IsImportableExternalBackupDirectory(DirectoryInfo dir)
        {
            try
            {
                if (File.Exists(Path.Combine(dir.FullName, "defaultES2File.txt")))
                    return true;

                return Directory.GetFiles(dir.FullName, "*", SearchOption.AllDirectories).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private string GetExternalBackupName(string externalBackupPath)
        {
            string name = Path.GetFileName(externalBackupPath);
            if (File.Exists(externalBackupPath) && Path.GetExtension(externalBackupPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(externalBackupPath);
            return name;
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
                return 0;
            }
            
            string[] backups = GetExternalBackupPaths();
            if (backups.Length == 0)
            {
                return 0;
            }
            
            int importedCount = 0;
            bool allImported = true;
            foreach (string externalBackupPath in backups)
            {
                string backupName = GetExternalBackupName(externalBackupPath);
                string importedPrefix = LocalizationManager.Text("IMPORTED - ", "IMPORTADO - ");
                if (localizationManager != null)
                {
                    importedPrefix = localizationManager.GetImportedPrefix();
                }
                string importedName = importedPrefix + backupName;
                
                if (backupManager.ImportExternalBackupPath(externalBackupPath, importedName, backupLimit, false))
                {
                    importedCount++;
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
                // Log informando importação concluída
                string completedStart = LocalizationManager.Text("[BackupSave] Total of ", "[BackupSave] Total de ");
                string completedEnd = LocalizationManager.Text(" backup(s) imported successfully.\nBackup folder deleted.\nThe backup list has been updated.", " backup(s) importado(s) com sucesso.\nPasta de backup excluída.\nA lista de backups foi atualizada.");
                if (localizationManager != null)
                {
                    completedStart = localizationManager.GetString("message", "importCompleted", completedStart);
                    completedEnd = localizationManager.GetString("message", "importCompletedEnd", completedEnd);
                }
                string completedMsg = completedStart + importedCount + completedEnd;
                ModConsole.Log("<color=#00ff00>" + completedMsg + "</color>");
            }
            else if (importedCount > 0)
            {
                string partialMsg = LocalizationManager.Text("[BackupSave] Some backups were imported, but a few failed.\nThe original folder was kept so you can try again.", "[BackupSave] Alguns backups foram importados, mas houve falhas.\nA pasta original foi mantida para você tentar novamente.");
                if (localizationManager != null)
                {
                    partialMsg = localizationManager.GetString("message", "importPartial", partialMsg);
                }
                ModConsole.Log("<color=#ffaa00>" + partialMsg + "</color>");
            }
            else if (!allImported)
            {
                string failMsg = LocalizationManager.Text("[BackupSave] Failed to import backups.\nThe original folder was kept so you can try again.", "[BackupSave] Falha ao importar os backups.\nA pasta original foi mantida para você tentar novamente.");
                if (localizationManager != null)
                {
                    failMsg = localizationManager.GetString("message", "importFailed", failMsg);
                }
                ModConsole.Error(failMsg);
            }
            
            return importedCount;
        }
    }
}
