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
        
        private readonly string[] filesToImport = { "defaultES2File.txt", "graveyard.txt", "items.txt", "notepad.txt", "options.txt", "trophies.txt" };

        public SaveImportManager(string mscSavesPath, BackupManager backupManager = null)
        {
            this.mscSavesPath = mscSavesPath;
            this.backupManager = backupManager;
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
        /// Delete all files in MWC save folder
        /// </summary>
        private void DeleteAllSaveFiles(string mwcSavePath)
        {
            if (Directory.Exists(mwcSavePath))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(mwcSavePath);
                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    file.Delete();
                }
            }
        }

        /// <summary>
        /// Copy all save files from MSC to MWC (only files that exist)
        /// </summary>
        private void CopyAllSaveFiles(string mscSavePath, string mwcSavePath)
        {
            foreach (string fileName in filesToImport)
            {
                string sourceFile = Path.Combine(mscSavePath, fileName);
                string destFile = Path.Combine(mwcSavePath, fileName);
                
                // Only copy if the file exists in source
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, destFile);
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
                    ModUI.ShowMessage("Erro: Nenhum save encontrado no My Summer Car!", "FALHA");
                    return;
                }

                bool mwcHasValidSave = backupManager.ValidateSaveFiles("My Winter Car");
                bool backupCreated = false;
                
                if (mwcHasValidSave)
                {
                    ModConsole.Log("<color=#ffffff>[BackupSave] Criando backup do save atual do My Winter Car...</color>");
                    backupCreated = backupManager.DoBackup("My Winter Car", customBackupName, backupLimit);
                }

                if (!Directory.Exists(mwcSavePath))
                    Directory.CreateDirectory(mwcSavePath);

                DeleteAllSaveFiles(mwcSavePath);
                CopyAllSaveFiles(mscSavePath, mwcSavePath);

                ModConsole.Log("<color=#00ff00>[BackupSave] Save importado com sucesso do My Summer Car para o My Winter Car!</color>");
                
                string message = "Save do My Summer Car foi importado para o My Winter Car.";
                if (!backupCreated && mwcHasValidSave)
                    message += "\n\nAVISO: Falha ao criar backup do save anterior.";
                else if (!mwcHasValidSave)
                    message += "\nAviso: Não havia save anterior para backup.";
                    
                ModUI.ShowMessage(backupCreated ? "Backup criado com sucesso!\n" + message : message, backupCreated ? "SUCESSO" : "SUCESSO COM AVISO");
            }
            catch (Exception ex)
            {
                ModUI.ShowMessage("Erro ao importar save: " + ex.Message, "FALHA");
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
                return 0;
            
            string[] backups = GetExternalBackupList();
            if (backups.Length == 0)
                return 0;
            
            int importedCount = 0;
            foreach (string backupName in backups)
            {
                string externalBackupPath = Path.Combine(externalBackupRootPath, backupName);
                string importedName = "IMPORTADO - " + backupName;
                
                // Usar BackupManager para criar o backup importado
                backupManager.ImportExternalBackupPath(externalBackupPath, importedName, backupLimit);
                importedCount++;
                
                ModConsole.Log("<color=#00ff00>[BackupSave] Backup externo importado: " + importedName + "</color>");
            }
            
            // Aplicar limite de backups após importar todos
            backupManager.ManageBackupLimit("My Summer Car", backupLimit);
            
            // Deletar pasta Backup externa após importação bem-sucedida
            if (importedCount > 0)
            {
                Directory.Delete(externalBackupRootPath, true);
                ModConsole.Log("<color=#00ff00>[BackupSave] Pasta de backups externos deletada com sucesso.</color>");
                
                // Pop-up informando importação concluída
                ModUI.ShowMessage("Total de " + importedCount + " backup(s) importado(s) com sucesso!\nPasta de backups deletada.\nFeche e abra o jogo para que os backups apareçam na lista.", "IMPORTAÇÃO CONCLUÍDA");
            }
            
            return importedCount;
        }
    }
}
