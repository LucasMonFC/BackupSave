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
        private const string MSC_GAME_FOLDER = "My Summer Car";
        private const string MWC_GAME_FOLDER = "My Winter Car";
        
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
                    ModConsole.Error(LogFormatter.WithPrefixEachLine(errorMsg));
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
                
                ModConsole.Log("<color=#00ff00>" + LogFormatter.WithPrefixEachLine(message) + "</color>");
            }
            catch (Exception ex)
            {
                string errorPrefix = LocalizationManager.Text("Error importing save: ", "Erro ao importar save: ");
                if (localizationManager != null)
                {
                    errorPrefix = localizationManager.GetString("message", "errorImport", errorPrefix);
                }
                ModConsole.Error(LogFormatter.WithPrefixEachLine(errorPrefix + ex.Message));
            }
        }

        /// <summary>
        /// Check if MSC save exists (checks for the key file)
        /// </summary>
        public bool HasMSCSave() => HasKeyFile(Path.Combine(mscSavesPath, "My Summer Car"));

        /// <summary>
        /// Verifica se existem backups externos importáveis
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

        private string GetCurrentGameFolder()
        {
            return ModLoader.CurrentGame == Game.MySummerCar ? MSC_GAME_FOLDER : MWC_GAME_FOLDER;
        }

        /// <summary>
        /// Lista todos os backups externos importáveis
        /// </summary>
        private string[] GetExternalBackupPaths()
        {
            try
            {
                List<FileSystemInfo> backups = new List<FileSystemInfo>();
                foreach (string externalBackupPath in GetExternalBackupRootPaths())
                    AddExternalBackupsFromRoot(externalBackupPath, backups, IsDirectoryBackupRoot(externalBackupPath));

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

        private string[] GetExternalBackupRootPaths()
        {
            List<string> paths = new List<string>();

            string currentGameFolder = GetCurrentGameFolder();
            if (!string.IsNullOrEmpty(mscSavesPath))
            {
                AddUniquePath(paths, Path.Combine(Path.Combine(mscSavesPath, currentGameFolder), "backups"));
                if (currentGameFolder == MSC_GAME_FOLDER)
                    AddUniquePath(paths, Path.Combine(mscSavesPath, "Backup"));

                string gameCode = GetCurrentGameCode();
                AddUniquePath(paths, Path.Combine(Path.Combine(mscSavesPath, "Backup"), gameCode + "_Backup"));
                AddUniquePath(paths, Path.Combine(Path.Combine(mscSavesPath, "RestorePoints"), gameCode + "_RestorePoints"));
            }

            return paths.ToArray();
        }

        private string GetCurrentGameCode()
        {
            return GetCurrentGameFolder() == MSC_GAME_FOLDER ? "MSC" : "MWC";
        }

        private void AddUniquePath(List<string> paths, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], path, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            paths.Add(path);
        }

        private bool IsLegacySaveBackuperRoot(string externalBackupPath)
        {
            if (string.IsNullOrEmpty(mscSavesPath) || string.IsNullOrEmpty(externalBackupPath))
                return false;

            string legacyRoot = Path.Combine(mscSavesPath, "Backup");
            return string.Equals(
                Path.GetFullPath(externalBackupPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(legacyRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDirectoryBackupRoot(string externalBackupPath)
        {
            return IsLegacySaveBackuperRoot(externalBackupPath) || IsSeasonalAutoBackupRoot(externalBackupPath);
        }

        private bool IsSeasonalAutoBackupRoot(string externalBackupPath)
        {
            if (string.IsNullOrEmpty(mscSavesPath) || string.IsNullOrEmpty(externalBackupPath))
                return false;

            string gameCode = GetCurrentGameCode();
            string backupRoot = Path.Combine(Path.Combine(mscSavesPath, "Backup"), gameCode + "_Backup");
            string restoreRoot = Path.Combine(Path.Combine(mscSavesPath, "RestorePoints"), gameCode + "_RestorePoints");
            string normalizedPath = NormalizePath(externalBackupPath);
            return string.Equals(normalizedPath, NormalizePath(backupRoot), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedPath, NormalizePath(restoreRoot), StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private void AddExternalBackupsFromRoot(string externalBackupPath, List<FileSystemInfo> backups, bool includeDirectories)
        {
            if (!Directory.Exists(externalBackupPath))
                return;

            DirectoryInfo root = new DirectoryInfo(externalBackupPath);
            if (includeDirectories)
            {
                foreach (DirectoryInfo dir in root.GetDirectories())
                {
                    if (IsExternalBackupContainerDirectory(dir.Name))
                        continue;
                    if (IsImportableExternalBackupDirectory(dir))
                        backups.Add(dir);
                }
            }

            foreach (FileInfo file in root.GetFiles("*.zip"))
                backups.Add(file);
        }

        private bool IsExternalBackupContainerDirectory(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName))
                return false;

            return directoryName.Equals("MSC_Backup", StringComparison.OrdinalIgnoreCase)
                || directoryName.Equals("MWC_Backup", StringComparison.OrdinalIgnoreCase)
                || directoryName.Equals("MSC_RestorePoints", StringComparison.OrdinalIgnoreCase)
                || directoryName.Equals("MWC_RestorePoints", StringComparison.OrdinalIgnoreCase);
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
        /// Importa todos os backups externos para pontos de restauração do BackupSave
        /// </summary>
        public int ImportAllExternalBackups(int backupLimit)
        {
            if (backupManager == null)
                return 0;

            string[] backups = GetExternalBackupPaths();
            if (backups.Length == 0)
            {
                return 0;
            }
            
            string gameFolder = GetCurrentGameFolder();
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
                
                if (backupManager.ImportExternalBackupPath(gameFolder, externalBackupPath, importedName, backupLimit, false, true))
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
                    ModConsole.Error(LogFormatter.WithPrefixEachLine(errorMsg + ": " + importedName));
                }
            }

            // Deletar pastas externas após importação bem-sucedida
            if (importedCount > 0 && allImported)
            {
                DeleteExternalBackupRoots();
                // Log informando importação concluída
                string completedStart = LocalizationManager.Text("[BackupSave] Total of ", "[BackupSave] Total de ");
                string completedEnd = LocalizationManager.Text(" backup(s) imported as restore points successfully.\nExternal backup folder deleted.\nThe backup list has been updated.", " backup(s) importado(s) como ponto(s) de restauração com sucesso.\nPasta de backup externa excluída.\nA lista de backups foi atualizada.");
                if (localizationManager != null)
                {
                    completedStart = localizationManager.GetString("message", "importCompleted", completedStart);
                    completedEnd = localizationManager.GetString("message", "importCompletedEnd", completedEnd);
                }
                string completedMsg = LogFormatter.WithPrefixEachLine(completedStart + importedCount + completedEnd);
                ModConsole.Log("<color=#00ff00>" + completedMsg + "</color>");
            }
            else if (importedCount > 0)
            {
                string partialMsg = LocalizationManager.Text("[BackupSave] Some backups were imported, but a few failed.\nThe original folder was kept so you can try again.", "[BackupSave] Alguns backups foram importados, mas houve falhas.\nA pasta original foi mantida para você tentar novamente.");
                if (localizationManager != null)
                {
                    partialMsg = localizationManager.GetString("message", "importPartial", partialMsg);
                }
                ModConsole.Log("<color=#ffaa00>" + LogFormatter.WithPrefixEachLine(partialMsg) + "</color>");
            }
            else if (!allImported)
            {
                string failMsg = LocalizationManager.Text("[BackupSave] Failed to import backups.\nThe original folder was kept so you can try again.", "[BackupSave] Falha ao importar os backups.\nA pasta original foi mantida para você tentar novamente.");
                if (localizationManager != null)
                {
                    failMsg = localizationManager.GetString("message", "importFailed", failMsg);
                }
                ModConsole.Error(LogFormatter.WithPrefixEachLine(failMsg));
            }
            
            return importedCount;
        }

        private void DeleteExternalBackupRoots()
        {
            foreach (string externalBackupRootPath in GetExternalBackupRootPaths())
            {
                try
                {
                    if (Directory.Exists(externalBackupRootPath))
                        Directory.Delete(externalBackupRootPath, true);
                }
                catch { }
            }

            DeleteEmptyExternalParentFolder("Backup");
            DeleteEmptyExternalParentFolder("RestorePoints");
        }

        private void DeleteEmptyExternalParentFolder(string folderName)
        {
            try
            {
                if (string.IsNullOrEmpty(mscSavesPath))
                    return;

                string path = Path.Combine(mscSavesPath, folderName);
                if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                    Directory.Delete(path, false);
            }
            catch { }
        }
    }
}
