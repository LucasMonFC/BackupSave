using MSCLoader;
using System;
using System.Collections.Generic;
using System.IO;

namespace BackupSave
{
    public class BackupManager
    {
        private const string GRAVEYARD_FILE = "graveyard.txt";
        private const string LOG_PREFIX = "[BackupSave] ";
        private string mscSavesPath;

        public BackupManager(string mscSavesPath)
        {
            this.mscSavesPath = mscSavesPath;
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
        public string GetRestorePointPath(string gameFolder, string restorePointName = "") => GetTypePath(gameFolder, "Ponto de Restauração", restorePointName);

        private void CopyFilesInDirectory(string sourceDir, string destDir, bool skipGraveyard = false)
        {
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    if (skipGraveyard && fileName.ToLower() == GRAVEYARD_FILE)
                        continue;
                    string destFile = Path.Combine(destDir, fileName);
                    if (File.Exists(destFile)) File.Delete(destFile);
                    File.Copy(file, destFile);
                }
                catch (Exception ex)
                {
                    ModConsole.Error(LOG_PREFIX + "Erro ao copiar arquivo: " + ex.Message);
                }
            }
        }

        private void DeleteSaveFiles(string savePath, bool preserveGraveyard = false)
        {
            try
            {
                foreach (string file in Directory.GetFiles(savePath))
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
            }
            catch { }
        }

        public bool DoBackup(string gameFolder, string customBackupName, int backupLimit = 50, string characterFirstName = "")
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss");
            string prefix = string.IsNullOrEmpty(characterFirstName) ? "" : characterFirstName + " - ";
            string backupFolderName = prefix + (string.IsNullOrEmpty(customBackupName) ? timestamp : customBackupName + " - " + timestamp);
            return CreateBackupOrRestorePoint(GetBackupPath(gameFolder, backupFolderName), gameFolder, backupFolderName, false, backupLimit);
        }

        public string CreateRestorePoint(string gameFolder, string restorePointName)
        {
            if (!ValidateSaveFiles(gameFolder))
            {
                ModConsole.Log("<color=#ffaa00>" + LOG_PREFIX + "Erro ao criar ponto de restauração: Nenhum arquivo de save válido encontrado!</color>");
                return null;
            }
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss");
            string rpName = string.IsNullOrEmpty(restorePointName) ? timestamp : restorePointName + " - " + timestamp;
            return CreateBackupOrRestorePoint(GetRestorePointPath(gameFolder, rpName), gameFolder, rpName, true, 0) ? rpName : null;
        }

        private bool CreateBackupOrRestorePoint(string folderPath, string gameFolder, string name, bool isRestorePoint, int backupLimit)
        {
            try
            {
                Directory.CreateDirectory(folderPath);
                CopyFilesInDirectory(GetSavePath(gameFolder), folderPath);
                string type = isRestorePoint ? "Ponto de Restauração" : "Backup";
                ModConsole.Log("<color=#00ff00>" + LOG_PREFIX + type + " criado com sucesso - Nome: " + name + "</color>");
                if (!isRestorePoint) ManageBackupLimit(gameFolder, backupLimit);
                return true;
            }
            catch (Exception ex)
            {
                string type = isRestorePoint ? "ponto de restauração" : "backup";
                ModConsole.Error(LOG_PREFIX + "Erro ao criar " + type + "\n" + ex.Message);
                return false;
            }
        }

        public void ManageBackupLimit(string gameFolder, int backupLimit)
        {
            if (backupLimit <= 0) return;
            DirectoryInfo backupDir = new DirectoryInfo(GetBackupPath(gameFolder));
            if (!backupDir.Exists) return;
            DirectoryInfo[] backups = backupDir.GetDirectories();
            if (backups.Length <= backupLimit) return;
            List<DirectoryInfo> list = new List<DirectoryInfo>(backups);
            list.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));
            for (int i = 0; i < list.Count - backupLimit; i++)
            {
                try
                {
                    list[i].Delete(true);
                    ModConsole.Log("<color=#ffaa00>" + LOG_PREFIX + "Backup antigo deletado: " + list[i].Name + "</color>");
                }
                catch
                {
                    ModConsole.Error(LOG_PREFIX + "Erro ao deletar backup antigo: " + list[i].Name);
                }
            }
            ModConsole.Log("<color=#ffaa00>" + LOG_PREFIX + "Limite de backups aplicado. Mantendo apenas os " + backupLimit + " mais recentes.</color>");
        }

        public bool RestoreLatestBackupAuto(string gameFolder, bool preserveGraveyard = false)
        {
            DirectoryInfo backupDir = new DirectoryInfo(GetBackupPath(gameFolder));
            if (!backupDir.Exists) return false;
            DirectoryInfo[] backups = backupDir.GetDirectories();
            if (backups.Length == 0) return false;
            System.Array.Sort(backups, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
            return RestoreBackup(GetSavePath(gameFolder), backups[0].FullName, backups[0].Name, preserveGraveyard);
        }

        public bool RestoreBackupByName(string gameFolder, string backupName)
        {
            string backupPath = GetBackupPath(gameFolder, backupName);
            return Directory.Exists(backupPath) ? RestoreBackup(GetSavePath(gameFolder), backupPath, backupName, false) : false;
        }

        private bool RestoreBackup(string savePath, string backupPath, string backupName, bool preserveGraveyard)
        {
            try
            {
                DeleteSaveFiles(savePath, preserveGraveyard);
                CopyFilesInDirectory(backupPath, savePath, preserveGraveyard);
                ModConsole.Log("<color=#00ff00>" + LOG_PREFIX + "Backup restaurado com sucesso: " + backupName + "</color>");
                return true;
            }
            catch (Exception ex)
            {
                ModConsole.Error(LOG_PREFIX + "Erro crítico ao restaurar backup: " + ex.Message);
                return false;
            }
        }

        private bool DeleteBackupOrRestorePoint(string itemPath, string itemName, string type)
        {
            try
            {
                if (!Directory.Exists(itemPath)) return false;
                Directory.Delete(itemPath, true);
                ModConsole.Log("<color=#ffaa00>" + LOG_PREFIX + type + " deletado: " + itemName + "</color>");
                return true;
            }
            catch (Exception ex)
            {
                ModConsole.Error(LOG_PREFIX + "Erro ao deletar " + type.ToLower() + "\n" + ex.Message);
                return false;
            }
        }

        public bool DeleteBackupByName(string gameFolder, string backupName) => DeleteBackupOrRestorePoint(GetBackupPath(gameFolder, backupName), backupName, "Backup");

        public bool RestoreRestorePointByName(string gameFolder, string restorePointName)
        {
            string rpPath = GetRestorePointPath(gameFolder, restorePointName);
            return Directory.Exists(rpPath) ? RestoreBackup(GetSavePath(gameFolder), rpPath, restorePointName, false) : false;
        }

        public bool DeleteRestorePointByName(string gameFolder, string restorePointName) => DeleteBackupOrRestorePoint(GetRestorePointPath(gameFolder, restorePointName), restorePointName, "Ponto de Restauração");

        public bool DeleteMeshSaveFile(string gameFolder)
        {
            try
            {
                string meshSaveFile = Path.Combine(GetSavePath(gameFolder), "meshsave.txt");
                if (!File.Exists(meshSaveFile)) return false;
                File.Delete(meshSaveFile);
                ModConsole.Log("<color=#00ff00>" + LOG_PREFIX + "Arquivo meshsave.txt deletado com sucesso!</color>");
                return true;
            }
            catch (Exception ex)
            {
                ModConsole.Error(LOG_PREFIX + "Erro ao deletar meshsave.txt\n" + ex.Message);
                return false;
            }
        }

        private string[] GetDirectoryList(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return new string[] { };
                DirectoryInfo[] dirs = new DirectoryInfo(dirPath).GetDirectories();
                if (dirs.Length == 0) return new string[] { };
                System.Array.Sort(dirs, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                List<string> names = new List<string>();
                foreach (var dir in dirs) names.Add(dir.Name);
                return names.ToArray();
            }
            catch { return new string[] { }; }
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
                string content = TryDecodeFile(File.ReadAllBytes(filePath));
                int index = content.IndexOf("PlayerFirstName", System.StringComparison.OrdinalIgnoreCase);
                if (index == -1) return "";
                index += "PlayerFirstName".Length;
                while (index < content.Length && !char.IsLetter(content[index])) index++;
                if (index >= content.Length) return "";
                int end = index;
                while (end < content.Length && (char.IsLetter(content[end]) || content[end] == ' ' || content[end] == '-' || content[end] == '\'')) end++;
                string firstName = System.Text.RegularExpressions.Regex.Replace(content.Substring(index, end - index).Trim(), "\\s+", " ").Trim();
                return firstName.Length > 1 ? firstName : "";
            }
            catch (Exception ex) { ModConsole.Error(LOG_PREFIX + "Erro ao extrair nome: " + ex.Message); return ""; }
        }

        private string TryDecodeFile(byte[] bytes)
        {
            try { return System.Text.Encoding.UTF8.GetString(bytes); }
            catch { try { return System.Text.Encoding.ASCII.GetString(bytes); } catch { return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes); } }
        }

        public bool ImportExternalBackupPath(string externalBackupPath, string importedName, int backupLimit)
        {
            try
            {
                if (!Directory.Exists(externalBackupPath)) return false;
                string backupPath = GetBackupPath("My Summer Car", importedName);
                Directory.CreateDirectory(backupPath);
                CopyFilesInDirectory(externalBackupPath, backupPath);
                ManageBackupLimit("My Summer Car", backupLimit);
                return true;
            }
            catch (Exception ex) { ModConsole.Error(LOG_PREFIX + "Erro ao importar backup externo\n" + ex.Message); return false; }
        }
    }
}