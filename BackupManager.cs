using MSCLoader;
using System;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

namespace BackupSave
{
    public class BackupManager
    {
        private const string GRAVEYARD_FILE = "graveyard.txt";
        private const string ZIP_EXTENSION = ".zip";
        private const string LOG_PREFIX = "[BackupSave] ";
        private const string MSC_GAME_FOLDER = "My Summer Car";
        private const string MWC_GAME_FOLDER = "My Winter Car";
        private const string MSC_SATSUMA_TURBOCHARGER_MOD_ID = "SatsumaTurboCharger";
        private const string MWC_TURBOCHARGER_MOD_ID = "MwcTurbocharger";
        private const string MOD_SETTINGS_BACKUP_ROOT = "_BackupSaveModSettings";
        private string mscSavesPath;
        private LocalizationManager localizationManager;

        public BackupManager(string mscSavesPath, LocalizationManager localizationManager = null)
        {
            this.mscSavesPath = mscSavesPath;
            this.localizationManager = localizationManager;
        }

        /// <summary>
        /// Obtém o nome da pasta de backup para o jogo (MSC/MWC)
        /// </summary>
        private string GetBackupFolderName(string gameFolder)
        {
            return gameFolder == "My Summer Car" ? "MSC BackupSave" : "MWC BackupSave";
        }

        /// <summary>
        /// Obtém o caminho da pasta de save do jogo
        /// </summary>
        private string GetSavePath(string gameFolder)
        {
            return Path.Combine(mscSavesPath, gameFolder);
        }

        /// <summary>
        /// Valida se os arquivos chave de save existem para o jogo especificado
        /// MSC: defaultES2File.txt
        /// MWC: carparts.txt + savefile.txt
        /// </summary>
        public bool ValidateSaveFiles(string gameFolder)
        {
            string savePath = GetSavePath(gameFolder);
            
            if (gameFolder == "My Summer Car")
            {
                return File.Exists(Path.Combine(savePath, "defaultES2File.txt"));
            }
            else // My Winter Car
            {
                return File.Exists(Path.Combine(savePath, "savefile.txt"));
            }
        }

        private string GetTypePath(string gameFolder, string type, string itemName = "")
        {
            string backupFolderName = GetBackupFolderName(gameFolder);
            string basePath = Path.Combine(Path.Combine(Path.Combine(mscSavesPath, "BackupSave"), backupFolderName), type);
            return string.IsNullOrEmpty(itemName) ? basePath : Path.Combine(basePath, itemName);
        }

        public string GetBackupPath(string gameFolder, string backupName = "") => GetTypePath(gameFolder, "Backups", backupName);
        public string GetRestorePointPath(string gameFolder, string restorePointName = "")
        {
            string restorePointFolderName = LocalizationManager.Text("Restoration Point", "Ponto de Restauração");
            if (localizationManager != null)
            {
                restorePointFolderName = localizationManager.GetString("label", "restorePointFolder", restorePointFolderName);
            }
            return GetTypePath(gameFolder, restorePointFolderName, restorePointName);
        }

        /// <summary>
        /// Obtém o caminho da pasta raiz de BackupSave para o jogo (MSC BackupSave ou MWC BackupSave)
        /// </summary>
        public string GetBackupRootPath(string gameFolder)
        {
            string backupFolderName = GetBackupFolderName(gameFolder);
            return Path.Combine(Path.Combine(mscSavesPath, "BackupSave"), backupFolderName);
        }

        private void CopyFilesInDirectory(string sourceDir, string destDir, bool skipGraveyard = false)
        {
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    if (skipGraveyard && fileName.ToLower() == GRAVEYARD_FILE)
                        continue;
                    string relativePath = GetRelativePath(sourceDir, file);
                    if (IsInternalBackupPath(relativePath))
                        continue;
                    string destFile = Path.Combine(destDir, relativePath);
                    string destParent = Path.GetDirectoryName(destFile);
                    if (!Directory.Exists(destParent))
                        Directory.CreateDirectory(destParent);
                    if (File.Exists(destFile)) File.Delete(destFile);
                    File.Copy(file, destFile);
                }
                catch (Exception ex)
                {
                    ModConsole.Error(LocalizationManager.Text(
                        LOG_PREFIX + "Error copying file: ",
                        LOG_PREFIX + "Erro ao copiar arquivo: ") + ex.Message);
                }
            }
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            string normalizedBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedFull = Path.GetFullPath(fullPath);
            return normalizedFull.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)
                ? normalizedFull.Substring(normalizedBase.Length)
                : Path.GetFileName(fullPath);
        }

        private string GetZipPath(string itemPath)
        {
            return itemPath.EndsWith(ZIP_EXTENSION, StringComparison.OrdinalIgnoreCase) ? itemPath : itemPath + ZIP_EXTENSION;
        }

        private string GetItemNameWithoutZip(string name)
        {
            return name.EndsWith(ZIP_EXTENSION, StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(name) : name;
        }

        private bool BackupItemExists(string itemPath)
        {
            return Directory.Exists(itemPath) || File.Exists(GetZipPath(itemPath));
        }

        private string ResolveBackupItemPath(string itemPath)
        {
            string zipPath = GetZipPath(itemPath);
            if (Directory.Exists(itemPath) && File.Exists(zipPath))
            {
                DirectoryInfo directory = new DirectoryInfo(itemPath);
                FileInfo zipFile = new FileInfo(zipPath);
                return zipFile.LastWriteTime > directory.LastWriteTime ? zipPath : itemPath;
            }

            if (Directory.Exists(itemPath)) return itemPath;
            return File.Exists(zipPath) ? zipPath : itemPath;
        }

        private void CreateZipFromSaveFiles(string sourceDir, string zipPath, string gameFolder = "")
        {
            string parent = Path.GetDirectoryName(zipPath);
            if (!Directory.Exists(parent))
                Directory.CreateDirectory(parent);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using (ZipFile zip = new ZipFile())
            {
                foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    zip.AddFile(file, Path.GetDirectoryName(GetRelativePath(sourceDir, file)) ?? "");
                }

                AddSupportedModSettingsToZip(zip, gameFolder);
                zip.Save(zipPath);
            }
        }

        private void ExtractZipToDirectory(string zipPath, string destDir, bool skipGraveyard = false)
        {
            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (entry.IsDirectory)
                        continue;

                    string entryName = entry.FileName.Replace('/', Path.DirectorySeparatorChar);
                    string fileName = Path.GetFileName(entryName);
                    if (string.IsNullOrEmpty(fileName) || entryName.Contains("..") || Path.IsPathRooted(entryName))
                        continue;
                    if (IsInternalBackupPath(entryName))
                        continue;

                    if (skipGraveyard && fileName.ToLower() == GRAVEYARD_FILE)
                        continue;

                    entry.Extract(destDir, ExtractExistingFileAction.OverwriteSilently);
                }
            }
        }

        private void CopyOrExtractBackupItem(string itemPath, string destDir, bool skipGraveyard = false)
        {
            if (Directory.Exists(itemPath))
            {
                CopyFilesInDirectory(itemPath, destDir, skipGraveyard);
                return;
            }

            ExtractZipToDirectory(itemPath, destDir, skipGraveyard);
        }

        private bool IsInternalBackupPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            return normalized.Equals(MOD_SETTINGS_BACKUP_ROOT, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(MOD_SETTINGS_BACKUP_ROOT + "/", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldBackupModSettings(string gameFolder, string supportedGameFolder, string modId)
        {
            string settingsPath = GetModSettingsPath(supportedGameFolder, modId);
            return string.Equals(gameFolder, supportedGameFolder, StringComparison.OrdinalIgnoreCase)
                && IsModLoaded(modId)
                && Directory.Exists(settingsPath);
        }

        private bool IsModLoaded(string modId)
        {
            if (string.IsNullOrEmpty(modId) || ModLoader.LoadedMods == null)
                return false;

            foreach (Mod mod in ModLoader.LoadedMods)
            {
                if (mod != null && string.Equals(mod.ID, modId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void AddSupportedModSettingsToZip(ZipFile zip, string gameFolder)
        {
            AddModSettingsToZip(zip, gameFolder, MSC_GAME_FOLDER, MSC_SATSUMA_TURBOCHARGER_MOD_ID);
            AddModSettingsToZip(zip, gameFolder, MWC_GAME_FOLDER, MWC_TURBOCHARGER_MOD_ID);
        }

        private void AddModSettingsToZip(ZipFile zip, string gameFolder, string supportedGameFolder, string modId)
        {
            if (!ShouldBackupModSettings(gameFolder, supportedGameFolder, modId))
                return;

            string settingsPath = GetModSettingsPath(supportedGameFolder, modId);
            foreach (string file in Directory.GetFiles(settingsPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = GetRelativePath(settingsPath, file);
                string relativeDirectory = Path.GetDirectoryName(relativePath);
                string archiveDirectory = GetModSettingsBackupFolder(modId);
                if (!string.IsNullOrEmpty(relativeDirectory))
                    archiveDirectory += "/" + relativeDirectory.Replace('\\', '/');

                zip.AddFile(file, archiveDirectory);
            }
        }

        private void RestoreSupportedModSettings(string gameFolder, string backupPath)
        {
            RestoreModSettings(gameFolder, backupPath, MSC_GAME_FOLDER, MSC_SATSUMA_TURBOCHARGER_MOD_ID);
            RestoreModSettings(gameFolder, backupPath, MWC_GAME_FOLDER, MWC_TURBOCHARGER_MOD_ID);
        }

        private void RestoreModSettings(string gameFolder, string backupPath, string supportedGameFolder, string modId)
        {
            if (!string.Equals(gameFolder, supportedGameFolder, StringComparison.OrdinalIgnoreCase))
                return;
            if (!IsModLoaded(modId))
                return;

            if (Directory.Exists(backupPath))
            {
                string sourceDir = Path.Combine(Path.Combine(backupPath, MOD_SETTINGS_BACKUP_ROOT), modId);
                if (Directory.Exists(sourceDir))
                    ReplaceDirectoryContents(sourceDir, GetModSettingsPath(supportedGameFolder, modId));
                return;
            }

            if (File.Exists(backupPath))
                RestoreModSettingsFromZip(backupPath, supportedGameFolder, modId);
        }

        private void RestoreModSettingsFromZip(string zipPath, string gameFolder, string modId)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "BackupSave_" + modId + "_" + Guid.NewGuid().ToString("N"));
            bool hasSettings = false;

            try
            {
                Directory.CreateDirectory(tempDir);
                using (ZipFile zip = ZipFile.Read(zipPath))
                {
                    foreach (ZipEntry entry in zip)
                    {
                        string relativePath;
                        if (!TryGetModSettingsRelativePath(entry.FileName, modId, out relativePath))
                            continue;
                        if (string.IsNullOrEmpty(relativePath) || relativePath.Contains("..") || Path.IsPathRooted(relativePath))
                            continue;

                        entry.Extract(tempDir, ExtractExistingFileAction.OverwriteSilently);
                        if (!entry.IsDirectory)
                            hasSettings = true;
                    }
                }

                string extractedSettingsDir = Path.Combine(Path.Combine(tempDir, MOD_SETTINGS_BACKUP_ROOT), modId);
                if (hasSettings)
                    ReplaceDirectoryContents(extractedSettingsDir, GetModSettingsPath(gameFolder, modId));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }

        private bool TryGetModSettingsRelativePath(string archivePath, string modId, out string relativePath)
        {
            relativePath = "";
            if (string.IsNullOrEmpty(archivePath))
                return false;

            string normalized = archivePath.Replace('\\', '/').TrimStart('/');
            string prefix = GetModSettingsBackupFolder(modId) + "/";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            relativePath = normalized.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
            return true;
        }

        private string GetModSettingsBackupFolder(string modId)
        {
            return MOD_SETTINGS_BACKUP_ROOT + "/" + modId;
        }

        private string GetModSettingsPath(string gameFolder, string modId)
        {
            string[] candidates = GetModSettingsPathCandidates(gameFolder, modId);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (Directory.Exists(candidates[i]))
                    return candidates[i];
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string parent = Path.GetDirectoryName(candidates[i]);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    return candidates[i];
            }

            return candidates.Length > 0 ? candidates[0] : "";
        }

        private string[] GetModSettingsPathCandidates(string gameFolder, string modId)
        {
            List<string> candidates = new List<string>();

            AddModSettingsPathCandidate(candidates, GetCurrentGameRootPath(), modId);

            string documentsRoot = GetDocumentsPath();
            if (!string.IsNullOrEmpty(documentsRoot))
                AddModSettingsPathCandidate(candidates, Path.Combine(documentsRoot, GetDocumentsModFolderName(gameFolder)), modId);

            AddModSettingsPathCandidate(candidates, GetSavePath(gameFolder), modId);

            return candidates.ToArray();
        }

        private string GetDocumentsModFolderName(string gameFolder)
        {
            return string.Equals(gameFolder, MSC_GAME_FOLDER, StringComparison.OrdinalIgnoreCase)
                ? "MySummerCar"
                : "MyWinterCar";
        }

        private void AddModSettingsPathCandidate(List<string> candidates, string rootPath, string modId)
        {
            if (string.IsNullOrEmpty(rootPath))
                return;

            string path = BuildModSettingsPath(rootPath, modId);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], path, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            candidates.Add(path);
        }

        private string BuildModSettingsPath(string rootPath, string modId)
        {
            return Path.Combine(
                Path.Combine(
                    Path.Combine(
                        Path.Combine(rootPath, "Mods"),
                        "Config"),
                    "Mod Settings"),
                modId);
        }

        private string GetDocumentsPath()
        {
            try
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch { }

            return "";
        }

        private string GetCurrentGameRootPath()
        {
            try
            {
                string dataPath = UnityEngine.Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    DirectoryInfo dataDirectory = new DirectoryInfo(dataPath);
                    if (dataDirectory.Parent != null)
                        return dataDirectory.Parent.FullName;
                }
            }
            catch { }

            try
            {
                return Directory.GetCurrentDirectory();
            }
            catch { }

            return "";
        }

        private void ReplaceDirectoryContents(string sourceDir, string destDir)
        {
            if (string.IsNullOrEmpty(destDir) || !Directory.Exists(sourceDir))
                return;

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            else
                ClearDirectory(destDir);

            CopyFilesInDirectory(sourceDir, destDir);
        }

        private void ClearDirectory(string directory)
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                try { File.Delete(file); }
                catch { }
            }

            foreach (string childDirectory in Directory.GetDirectories(directory))
            {
                try { Directory.Delete(childDirectory, true); }
                catch { }
            }
        }

        private void DeleteSaveFiles(string savePath, bool preserveGraveyard = false)
        {
            try
            {
                foreach (string file in Directory.GetFiles(savePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        if (preserveGraveyard && fileName.ToLower() == GRAVEYARD_FILE)
                            continue;
                        File.Delete(file);
                    }
                    catch { }
                }

                string[] directories = Directory.GetDirectories(savePath, "*", SearchOption.AllDirectories);
                System.Array.Sort(directories, (a, b) => b.Length.CompareTo(a.Length));
                foreach (string directory in directories)
                {
                    try
                    {
                        if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                            Directory.Delete(directory, true);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public bool DoBackup(string gameFolder, string customBackupName, int backupLimit = 50, string characterFirstName = "")
        {
            if (!ValidateSaveFiles(gameFolder))
            {
                string errorMsg = LocalizationManager.Text(
                    "[BackupSave] Error creating backup: No valid save file found!",
                    "[BackupSave] Erro ao criar backup: nenhum arquivo de save válido foi encontrado!");
                if (localizationManager != null)
                {
                    errorMsg = localizationManager.GetString("log", "errorNoValidSave", errorMsg);
                }
                ModConsole.Log("<color=#ffaa00>" + errorMsg + "</color>");
                return false;
            }

            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss");
            string prefix = string.IsNullOrEmpty(characterFirstName) ? "" : characterFirstName + " - ";
            string backupFolderName = prefix + (string.IsNullOrEmpty(customBackupName) ? timestamp : customBackupName + " - " + timestamp);
            return CreateBackupOrRestorePoint(GetBackupPath(gameFolder, backupFolderName), gameFolder, false, backupLimit);
        }

        public string CreateRestorePoint(string gameFolder, string restorePointName)
        {
            if (!ValidateSaveFiles(gameFolder))
            {
                string errorMsg = LocalizationManager.Text(
                    "[BackupSave] Error creating restore point: No valid save file found!",
                    "[BackupSave] Erro ao criar ponto de restauração: nenhum arquivo de save válido foi encontrado!");
                if (localizationManager != null)
                {
                    errorMsg = localizationManager.GetString("log", "restorePointCreatedFailed", errorMsg);
                }
                ModConsole.Log("<color=#ffaa00>" + errorMsg + "</color>");
                return null;
            }
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss");
            string rpName = string.IsNullOrEmpty(restorePointName) ? timestamp : restorePointName + " - " + timestamp;
            return CreateBackupOrRestorePoint(GetRestorePointPath(gameFolder, rpName), gameFolder, true, 0) ? rpName : null;
        }

        private bool CreateBackupOrRestorePoint(string folderPath, string gameFolder, bool isRestorePoint, int backupLimit)
        {
            try
            {
                if (Directory.Exists(folderPath))
                    Directory.Delete(folderPath, true);

                CreateZipFromSaveFiles(GetSavePath(gameFolder), GetZipPath(folderPath), gameFolder);
                if (!isRestorePoint) ManageBackupLimit(gameFolder, backupLimit);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = isRestorePoint
                    ? LocalizationManager.Text("[BackupSave] Error creating restore point", "[BackupSave] Erro ao criar ponto de restauração")
                    : LocalizationManager.Text("[BackupSave] Error creating backup", "[BackupSave] Erro ao criar backup");
                if (localizationManager != null)
                {
                    errorMsg = isRestorePoint
                        ? localizationManager.GetString("log", "errorCreatingRestorePoint", errorMsg)
                        : localizationManager.GetString("log", "errorCreatingBackup", errorMsg);
                }
                ModConsole.Error(errorMsg + "\n" + ex.Message);
                return false;
            }
        }

        public void ManageBackupLimit(string gameFolder, int backupLimit)
        {
            if (backupLimit <= 0) return;
            DirectoryInfo backupDir = new DirectoryInfo(GetBackupPath(gameFolder));
            if (!backupDir.Exists) return;
            List<FileSystemInfo> list = GetBackupItems(GetBackupPath(gameFolder), true);
            if (list.Count <= backupLimit) return;
            list.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));
            for (int i = 0; i < list.Count - backupLimit; i++)
            {
                try
                {
                    DirectoryInfo directory = list[i] as DirectoryInfo;
                    if (directory != null)
                        directory.Delete(true);
                    else
                        list[i].Delete();
                }
                catch
                {
                    string errorMsg = LocalizationManager.Text("[BackupSave] Error deleting old backup: ", "[BackupSave] Erro ao deletar backup antigo: ");
                    if (localizationManager != null)
                    {
                        errorMsg = localizationManager.GetString("log", "errorDeletingOldBackup", errorMsg);
                    }
                    ModConsole.Error(errorMsg + GetItemNameWithoutZip(list[i].Name));
                }
            }
        }

        public bool RestoreLatestBackupAuto(string gameFolder, bool preserveGraveyard = false)
        {
            DirectoryInfo backupDir = new DirectoryInfo(GetBackupPath(gameFolder));
            if (!backupDir.Exists) return false;
            List<FileSystemInfo> backups = GetBackupItems(GetBackupPath(gameFolder), true);
            if (backups.Count == 0) return false;
            backups.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
            return RestoreBackup(gameFolder, GetSavePath(gameFolder), backups[0].FullName, preserveGraveyard);
        }

        public bool RestoreBackupByName(string gameFolder, string backupName)
        {
            string backupPath = GetBackupPath(gameFolder, backupName);
            string resolvedPath = ResolveBackupItemPath(backupPath);
            return BackupItemExists(backupPath) ? RestoreBackup(gameFolder, GetSavePath(gameFolder), resolvedPath, false) : false;
        }

        private bool RestoreBackup(string gameFolder, string savePath, string backupPath, bool preserveGraveyard)
        {
            try
            {
                DeleteSaveFiles(savePath, preserveGraveyard);
                CopyOrExtractBackupItem(backupPath, savePath, preserveGraveyard);
                RestoreSupportedModSettings(gameFolder, backupPath);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = LocalizationManager.Text("[BackupSave] Critical error restoring backup: ", "[BackupSave] Erro crítico ao restaurar backup: ");
                if (localizationManager != null)
                {
                    errorMsg = localizationManager.GetString("log", "errorRestoringBackup", errorMsg);
                }
                ModConsole.Error(errorMsg + ex.Message);
                return false;
            }
        }

        private bool DeleteBackupOrRestorePoint(string itemPath, bool isRestorePoint)
        {
            try
            {
                string resolvedPath = ResolveBackupItemPath(itemPath);
                if (Directory.Exists(resolvedPath))
                    Directory.Delete(resolvedPath, true);
                else if (File.Exists(resolvedPath))
                    File.Delete(resolvedPath);
                else
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                string typeEn = isRestorePoint ? LocalizationManager.Text("restore point", "ponto de restauração") : "backup";
                string errorMsg = LocalizationManager.Text("[BackupSave] Error deleting item: ", "[BackupSave] Erro ao deletar item: ");
                if (localizationManager != null)
                {
                    errorMsg = localizationManager.GetString("log", "errorDeletingItem", errorMsg);
                }
                ModConsole.Error(errorMsg + typeEn + "\n" + ex.Message);
                return false;
            }
        }

        public bool DeleteBackupByName(string gameFolder, string backupName) => DeleteBackupOrRestorePoint(GetBackupPath(gameFolder, backupName), false);

        public bool RestoreRestorePointByName(string gameFolder, string restorePointName)
        {
            string rpPath = GetRestorePointPath(gameFolder, restorePointName);
            string resolvedPath = ResolveBackupItemPath(rpPath);
            return BackupItemExists(rpPath) ? RestoreBackup(gameFolder, GetSavePath(gameFolder), resolvedPath, false) : false;
        }

        public bool DeleteRestorePointByName(string gameFolder, string restorePointName) => DeleteBackupOrRestorePoint(GetRestorePointPath(gameFolder, restorePointName), true);

        public bool DeleteMeshSaveFile(string gameFolder)
        {
            try
            {
                string meshSaveFile = Path.Combine(GetSavePath(gameFolder), "meshsave.txt");
                if (!File.Exists(meshSaveFile)) return false;
                File.Delete(meshSaveFile);
                return true;
            }
            catch (Exception ex)
            {
                ModConsole.Error(LocalizationManager.Text(
                    LOG_PREFIX + "Error deleting meshsave.txt\n",
                    LOG_PREFIX + "Erro ao deletar meshsave.txt\n") + ex.Message);
                return false;
            }
        }

        private List<FileSystemInfo> GetBackupItems(string dirPath, bool includeZipFiles)
        {
            List<FileSystemInfo> items = new List<FileSystemInfo>();
            if (!Directory.Exists(dirPath)) return items;

            DirectoryInfo directory = new DirectoryInfo(dirPath);
            Dictionary<string, FileSystemInfo> uniqueItems = new Dictionary<string, FileSystemInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (DirectoryInfo item in directory.GetDirectories())
                uniqueItems[GetItemNameWithoutZip(item.Name)] = item;

            if (includeZipFiles)
            {
                foreach (FileInfo item in directory.GetFiles("*" + ZIP_EXTENSION))
                {
                    string key = GetItemNameWithoutZip(item.Name);
                    if (!uniqueItems.ContainsKey(key) || item.LastWriteTime > uniqueItems[key].LastWriteTime)
                        uniqueItems[key] = item;
                }
            }

            foreach (FileSystemInfo item in uniqueItems.Values)
                items.Add(item);

            return items;
        }

        private string[] GetDirectoryList(string dirPath)
        {
            try
            {
                List<FileSystemInfo> items = GetBackupItems(dirPath, true);
                if (items.Count == 0) return new string[] { };
                items.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                List<string> names = new List<string>();
                foreach (FileSystemInfo item in items) names.Add(GetItemNameWithoutZip(item.Name));
                return names.ToArray();
            }
            catch { return new string[] { }; }
        }

        public string GetLatestBackupName(string gameFolder)
        {
            try
            {
                List<FileSystemInfo> backups = GetBackupItems(GetBackupPath(gameFolder), true);
                if (backups.Count == 0) return "";
                backups.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                return GetItemNameWithoutZip(backups[0].Name);
            }
            catch { return ""; }
        }

        /// <summary>
        /// Retorna lista de nomes de backups disponíveis (do mais recente para o mais antigo)
        /// </summary>
        public string[] GetBackupList(string gameFolder)
        {
            return GetDirectoryList(GetBackupPath(gameFolder));
        }

        /// <summary>
        /// Retorna lista de nomes de pontos de restauração disponíveis (do mais recente para o mais antigo)
        /// </summary>
        public string[] GetRestorePointList(string gameFolder)
        {
            return GetDirectoryList(GetRestorePointPath(gameFolder));
        }

        /// <summary>
        /// Extrai o primeiro nome do personagem do arquivo de save
        /// Para MSC: lê de defaultES2File.txt
        /// Para MWC: lê de savefile.txt
        /// Procura pelo padrão de PlayerFirstName
        /// </summary>
        public string GetCharacterFirstName(string gameFolder)
        {
            try
            {
                string filePath = Path.Combine(GetSavePath(gameFolder), gameFolder == "My Summer Car" ? "defaultES2File.txt" : "savefile.txt");
                if (!File.Exists(filePath)) return "";
                byte[] bytes = File.ReadAllBytes(filePath);
                string firstName = ExtractES2StringValue(bytes, "PlayerFirstName");
                if (!string.IsNullOrEmpty(firstName)) return firstName;

                string content = TryDecodeFile(bytes);
                int index = content.IndexOf("PlayerFirstName", System.StringComparison.OrdinalIgnoreCase);
                if (index == -1) return "";
                index += "PlayerFirstName".Length;
                while (index < content.Length && !IsPlainNameStartChar(content[index])) index++;
                if (index >= content.Length) return "";
                int end = index;
                while (end < content.Length && IsNameChar(content[end])) end++;
                firstName = CleanCharacterFirstName(content.Substring(index, end - index));
                return firstName.Length > 1 ? firstName : "";
            }
            catch (Exception ex) { ModConsole.Error(LocalizationManager.Text(LOG_PREFIX + "Error extracting name: ", LOG_PREFIX + "Erro ao extrair nome: ") + ex.Message); return ""; }
        }

        private string ExtractES2StringValue(byte[] bytes, string key)
        {
            byte[] keyBytes = System.Text.Encoding.ASCII.GetBytes(key);
            byte[] stringMarker = new byte[] { 0xFF, 0xEE, 0xF1, 0xE9, 0xFD };
            int keyIndex = IndexOfBytes(bytes, keyBytes, 0, bytes.Length);
            if (keyIndex < 0) return "";

            int searchStart = keyIndex + keyBytes.Length;
            int searchEnd = Math.Min(bytes.Length, searchStart + 64);
            int markerIndex = IndexOfBytes(bytes, stringMarker, searchStart, searchEnd);
            if (markerIndex < 0) return "";

            int lengthIndex = markerIndex + stringMarker.Length;
            if (lengthIndex >= bytes.Length) return "";

            int stringLength = bytes[lengthIndex];
            int valueStart = lengthIndex + 1;
            if (stringLength <= 0 || valueStart + stringLength > bytes.Length)
                return "";

            string value = DecodeStringBytes(bytes, valueStart, stringLength);
            return CleanCharacterFirstName(value);
        }

        private int IndexOfBytes(byte[] bytes, byte[] pattern, int startIndex, int endIndex)
        {
            int lastStart = Math.Min(endIndex, bytes.Length) - pattern.Length;
            for (int i = Math.Max(0, startIndex); i <= lastStart; i++)
            {
                bool matched = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (bytes[i + j] != pattern[j])
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched) return i;
            }
            return -1;
        }

        private string DecodeStringBytes(byte[] bytes, int index, int count)
        {
            try { return new System.Text.UTF8Encoding(false, true).GetString(bytes, index, count); }
            catch { return System.Text.Encoding.Default.GetString(bytes, index, count); }
        }

        private string CleanCharacterFirstName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string cleaned = System.Text.RegularExpressions.Regex.Replace(value.Trim(), "\\s+", " ").Trim();
            return cleaned.Length > 1 ? cleaned : "";
        }

        private bool IsPlainNameStartChar(char value)
        {
            return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }

        private bool IsNameChar(char value)
        {
            return IsPlainNameStartChar(value) || value == ' ' || value == '-' || value == '\'';
        }

        private string TryDecodeFile(byte[] bytes)
        {
            try { return new System.Text.UTF8Encoding(false, true).GetString(bytes); }
            catch { try { return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes); } catch { return System.Text.Encoding.ASCII.GetString(bytes); } }
        }

        public bool ImportExternalBackupPath(string externalBackupPath, string importedName, int backupLimit, bool manageLimit = true)
        {
            try
            {
                string backupPath = GetBackupPath("My Summer Car", importedName);
                string zipPath = GetZipPath(backupPath);

                if (Directory.Exists(externalBackupPath))
                {
                    CreateZipFromSaveFiles(externalBackupPath, zipPath);
                }
                else if (File.Exists(externalBackupPath) && Path.GetExtension(externalBackupPath).Equals(ZIP_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(zipPath);
                    if (!Directory.Exists(parent))
                        Directory.CreateDirectory(parent);
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                    File.Copy(externalBackupPath, zipPath);
                }
                else
                {
                    return false;
                }

                if (manageLimit)
                    ManageBackupLimit("My Summer Car", backupLimit);
                return true;
            }
            catch (Exception ex) { ModConsole.Error(LocalizationManager.Text(LOG_PREFIX + "Error importing external backup\n", LOG_PREFIX + "Erro ao importar backup externo\n") + ex.Message); return false; }
        }
    }
}
