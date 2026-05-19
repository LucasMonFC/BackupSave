using MSCLoader;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BackupSave
{
    public class LocalizationManager
    {
        public enum Language
        {
            PortuguesBrasil = 0,
            English = 1
        }

        private Language currentLanguage = Language.PortuguesBrasil;
        private Dictionary<string, string> englishTranslations = new Dictionary<string, string>();

        public LocalizationManager(string gameFolder = "")
        {
            InitializeTranslations();
            currentLanguage = DetectSystemLanguage();
            ModConsole.Print("[BackupSave] Idioma detectado automaticamente: " + (currentLanguage == Language.PortuguesBrasil ? "pt-BR" : "en-US"));
        }

        [DllImport("kernel32.dll")]
        private static extern int GetUserDefaultUILanguage();

        [DllImport("kernel32.dll")]
        private static extern int GetUserDefaultLCID();

        [DllImport("kernel32.dll")]
        private static extern int GetUserDefaultLangID();

        [DllImport("kernel32.dll")]
        private static extern int GetSystemDefaultUILanguage();

        private Language DetectSystemLanguage()
        {
            if (IsPortugueseLanguageId(GetUserDefaultUILanguage()) ||
                IsPortugueseLanguageId(GetUserDefaultLCID()) ||
                IsPortugueseLanguageId(GetUserDefaultLangID()) ||
                IsPortugueseLanguageId(GetSystemDefaultUILanguage()))
            {
                return Language.PortuguesBrasil;
            }

            return Language.English;
        }

        private bool IsPortugueseLanguageId(int languageId)
        {
            if (languageId <= 0)
                return false;

            const int PortuguesePrimaryLanguageId = 0x16;
            return (languageId & 0x3ff) == PortuguesePrimaryLanguageId;
        }

        public Language GetLanguage()
        {
            return currentLanguage;
        }

        public string GetLanguageName(Language language)
        {
            switch (language)
            {
                case Language.PortuguesBrasil: return "PORTUGUÊS (BRASIL)";
                case Language.English: return "ENGLISH";
                default: return "PORTUGUÊS (BRASIL)";
            }
        }

        public string GetString(string section, string key, string defaultValue = "")
        {
            if (currentLanguage == Language.English)
            {
                string fullKey = section + "." + key;
                if (englishTranslations.ContainsKey(fullKey))
                {
                    return englishTranslations[fullKey];
                }
            }
            return string.IsNullOrEmpty(defaultValue) ? section + "." + key : defaultValue;
        }

        public string[] GetAutoRestoreModeValues()
        {
            return new string[]
            {
                GetString("mode", "disabled", "Desligada"),
                GetString("mode", "restoreAll", "Restaurar Tudo"),
                GetString("mode", "restoreWithGraveyard", "Restaurar mantendo as lápides")
            };
        }

        public string GetAutoRestoreModeLabel()
        {
            if (currentLanguage == Language.English)
                return "Automatic Restoration: ";
            else
                return "Restauração Automática: ";
        }

        public string GetRestorePointFolderName()
        {
            return GetString("label", "restorePointFolder", "Ponto de Restauração");
        }

        public string GetImportedPrefix()
        {
            return GetString("label", "importedPrefix", "IMPORTADO - ");
        }

        private void AddTranslationSafe(string key, string value)
        {
            if (!englishTranslations.ContainsKey(key))
            {
                englishTranslations.Add(key, value);
            }
        }

        private void InitializeTranslations()
        {
            // Limpar dicionário para evitar duplicatas
            englishTranslations.Clear();
            
            // ===== HEADERS - ENGLISH ONLY =====
            AddTranslationSafe("header.backupsave", "BACKUPSAVE");
            AddTranslationSafe("header.settings", "SAVE SETTINGS");
            AddTranslationSafe("header.createRestorePoint", "CREATE RESTORE POINT");
            AddTranslationSafe("header.deleteMeshsave", "DELETE MESHSAVE FILE");
            AddTranslationSafe("header.importSave", "IMPORT SAVE");
            AddTranslationSafe("header.backups", "BACKUPS AND RESTORE POINTS");
            AddTranslationSafe("header.language", "SELECT LANGUAGE");

            // ===== BUTTONS - ENGLISH ONLY =====
            AddTranslationSafe("button.info", "<color=cyan>ℹ INFORMATION</color>");
            AddTranslationSafe("button.credits", "<color=yellow>★ CREDITS</color>");
            AddTranslationSafe("button.confirmLanguage", "CONFIRM LANGUAGE CHANGE");
            AddTranslationSafe("button.restore", "RESTORE");
            AddTranslationSafe("button.delete", "<color=red>DELETE</color>");
            AddTranslationSafe("button.restartMenu", "<color=cyan>RESTART MENU</color>");
            AddTranslationSafe("button.openBackupFolder", "<color=white>OPEN BACKUP FOLDER</color>");

            // ===== DESCRIPTIVE TEXTS - ENGLISH ONLY =====
            AddTranslationSafe("text.autoRestore", "<b>Automatic Restore Mode</b>\nThe mod detects when the save is deleted (death in Mortal Mode) and can automatically restore the latest backup.");
            AddTranslationSafe("text.backupLimit", "<b>Backup Limit Configuration</b>\nSet the maximum number of backups to keep. Old backups will be deleted automatically.");
            AddTranslationSafe("text.prefixCharName", "<b>Character Name Prefix</b>\nPrefix backups with the first name of the character to identify them easily.");
            AddTranslationSafe("text.backupsAndPoints", "<b>Backups and Restore Points</b>\nSelect a backup or restore point to restore/delete");
            AddTranslationSafe("text.noBackups", "You don't have any backups or restore points yet.");
            AddTranslationSafe("text.createRestorePointDesc", "Restore points are permanent and will not be deleted by backup limits.");
            AddTranslationSafe("text.deleteMeshsaveDesc", "Delete the meshsave.txt file to restore the vehicle format.");
            AddTranslationSafe("text.importSaveDesc", "Import your save from My Summer Car to My Winter Car with backup.");

            // ===== LABELS - ENGLISH ONLY =====
            AddTranslationSafe("label.language", "Mod Language");
            AddTranslationSafe("label.autoRestoreMode", "Restoration Mode");
            AddTranslationSafe("label.backupLimit", "Maximum Number of Backups to Store");
            AddTranslationSafe("label.selectSave", "Select a save or point");
            AddTranslationSafe("label.restorePointName", "Restore Point Name (optional)");
            AddTranslationSafe("label.deleteMeshsave", "Delete meshsave automatically");
            AddTranslationSafe("label.autoDeleteMeshsave", "Delete meshsave automatically");
            AddTranslationSafe("label.prefixCharacterName", "Prefix backup with character name");

            // ===== POPUPS SUCCESS - ENGLISH ONLY =====
            AddTranslationSafe("popup.successRestored", "Your backup has been successfully restored!");
            AddTranslationSafe("popup.successDeleted", "was successfully deleted!");
            AddTranslationSafe("popup.successDeletePoint", "Restore point was successfully deleted!");
            AddTranslationSafe("popup.successDeleteBackup", "Backup was successfully deleted!");
            AddTranslationSafe("popup.successDeleteMeshsave", "Meshsave.txt file was successfully deleted!");
            AddTranslationSafe("popup.successCreatePoint", "Restore point was successfully created!");

            // ===== POPUPS ERROR - ENGLISH ONLY =====
            AddTranslationSafe("popup.meshsaveNotFound", "Meshsave.txt file not found.");
            AddTranslationSafe("popup.failCreatePointNoSave", "Failed to create restore point!\nNo valid save file found.");

            // ===== POPUP TITLES - ENGLISH ONLY =====
            AddTranslationSafe("popup.titleSuccess", "SUCCESS");
            AddTranslationSafe("popup.titleFailure", "FAILURE");
            AddTranslationSafe("popup.titleWarning", "WARNING");
            AddTranslationSafe("popup.titleInfo", "MOD INFORMATION");
            AddTranslationSafe("popup.titleCredits", "CREDITS");
            AddTranslationSafe("popup.titleLanguageSelection", "LANGUAGE SELECTION");
            AddTranslationSafe("popup.titleImportCompleted", "IMPORT COMPLETED");
            AddTranslationSafe("popup.titleImportAlreadyDone", "IMPORT ALREADY COMPLETED");
            AddTranslationSafe("popup.restartGameMessage", "Language changed to English!\nPlease close and reopen the game for the changes to take effect.");

            // ===== INFO AND CREDITS - ENGLISH ONLY =====
            AddTranslationSafe("info.title", "<b>BackupSave - Backup System</b>");
            AddTranslationSafe("info.features", "<b>Features:</b>\n• <b>Automatic backups:</b> Every time you load the game a backup is created\n• <b>Automatic restore:</b> Detects death and restores the latest backup automatically\n• <b>Restore Points:</b> Create permanent points to restore whenever you want\n• <b>Limit Control:</b> Set how many backups to keep\n• <b>Delete meshsave:</b> Reset the vehicle format when needed");
            AddTranslationSafe("credits.basedOn", "<b>Based on:</b>\nSaveBackuper by AnimeForevere");
            AddTranslationSafe("credits.developedBy", "<b>Developed by:</b>\nLucasMonOficial");

            // ===== LOGS - ENGLISH ONLY =====
            AddTranslationSafe("log.errorCopyFile", "Error copying file");
            AddTranslationSafe("log.errorCreateBackup", "Error creating backup");
            AddTranslationSafe("log.errorNoValidSave", "No valid save file found!");
            AddTranslationSafe("log.errorDeleteOldBackup", "Error deleting old backup");
            AddTranslationSafe("log.errorRestoreBackup", "ERROR: Critical failure restoring backup");
            AddTranslationSafe("log.deletedOldBackup", "Deleted old backup");
            AddTranslationSafe("log.languageSaved", "Language saved successfully!");

            // ===== MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("message.languageChanged", "Language changed successfully!");
            AddTranslationSafe("message.creatingMWCBackup", "Creating backup of current My Winter Car save...");
            AddTranslationSafe("message.saveImported", "Save imported successfully from My Summer Car to My Winter Car!");
            AddTranslationSafe("message.successImport", "Backup created successfully!\nSave from My Summer Car was imported to My Winter Car.");
            AddTranslationSafe("message.successImportNoBackup", "Save from My Summer Car was imported to My Winter Car!\nWARNING: Failed to create backup of previous save.");
            AddTranslationSafe("message.successImportNoSave", "Save from My Summer Car was imported to My Winter Car!\nWARNING: No previous save to backup.");
            AddTranslationSafe("message.importSaveNotFound", "Error: No save found in My Summer Car!");
            AddTranslationSafe("message.importExternalSuccess", "Total of");
            AddTranslationSafe("message.importExternalSuccessEnd", "backup(s) imported successfully!\nBackup folder deleted.\nThe backup list has been updated.");
            AddTranslationSafe("message.importAlreadyDone", "Import has already been completed.\nThe backup list is already updated.");

            // ===== AUTO RESTORE MODES - ENGLISH ONLY =====
            AddTranslationSafe("mode.disabled", "Disabled");
            AddTranslationSafe("mode.restoreAll", "Restore All");
            AddTranslationSafe("mode.restoreWithGraveyard", "Restore keeping gravestones");
            AddTranslationSafe("log.autoRestoreModeDisabled", "[BackupSave] Automatic Restoration: Disabled");
            AddTranslationSafe("log.autoRestoreModeRestoreAll", "[BackupSave] Automatic Restoration: Restore All");
            AddTranslationSafe("log.autoRestoreModeRestoreGraveyard", "[BackupSave] Automatic Restoration: Restore keeping gravestones");

            // ===== EXTERNAL BACKUPS - ENGLISH ONLY =====
            AddTranslationSafe("header.importExternalBackups", "IMPORT SAVEBACKUPER BACKUPS");
            AddTranslationSafe("text.importExternalPath", "Import all backups from: C:\\Users\\{user}\\AppData\\LocalLow\\Amistech\\Backup");
            AddTranslationSafe("button.importAllBackups", "Import All Backups");
            AddTranslationSafe("text.importedPrefix", "Imported backups will receive the 'IMPORTED - ' prefix for better identification.");

            // ===== RESTORE/DELETE MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("popup.successRestorePoint", "Your restore point has been successfully restored!");
            AddTranslationSafe("popup.successRestoreBackup", "Your backup has been successfully restored!");
            AddTranslationSafe("popup.successDeletePointMsg", "Restore point was successfully deleted!");
            AddTranslationSafe("popup.successDeleteBackupMsg", "Backup was successfully deleted!");
            AddTranslationSafe("popup.failDeletePointMsg", "Restore point was deleted.");
            AddTranslationSafe("popup.failDeleteBackupMsg", "Backup was deleted.");
            AddTranslationSafe("popup.successDeleteMeshsaveMsg", "Meshsave.txt file was successfully deleted!");
            AddTranslationSafe("popup.notFoundMeshsave", "Meshsave.txt file not found.");
            AddTranslationSafe("popup.successCreatePointMsg", "Restore point was successfully created!");
            AddTranslationSafe("popup.failCreatePointMsg", "Failed to create restore point!\nNo valid save file found.");

            // ===== INFO AND CREDITS TEXT - ENGLISH ONLY =====
            AddTranslationSafe("text.infoTitle", "<b>BackupSave - Backup System</b>");
            AddTranslationSafe("text.infoFeatures", "<b>Features:</b>");
            AddTranslationSafe("text.infoAutoBackup", "• <b>Automatic backups:</b> Every time you load the game a backup is created");
            AddTranslationSafe("text.infoAutoRestore", "• <b>Automatic restore:</b> Detects death and restores the latest backup automatically");
            AddTranslationSafe("text.infoRestorePoints", "• <b>Restore Points:</b> Create permanent points to restore whenever you want");
            AddTranslationSafe("text.infoLimitControl", "• <b>Limit Control:</b> Set how many backups to keep");
            AddTranslationSafe("text.infoPrefixName", "• <b>Character Name Prefix:</b> Add the character's name to the backup name");
            AddTranslationSafe("text.infoMeshsave", "• <b>Delete meshsave:</b> Reset vehicle format when needed");
            AddTranslationSafe("text.infoImportSaveBackuper", "• <b>Import saves from SaveBackuper:</b> Import your saves from the SaveBackuper MOD");
            AddTranslationSafe("text.infoImportSaveMWC", "• <b>Import Save:</b> Import your save from My Summer Car to My Winter Car");
            AddTranslationSafe("text.infoModes", "<b>Restoration Modes:</b>");
            AddTranslationSafe("text.infoDisabled", "• <b>Disabled:</b> Does not restore automatically");
            AddTranslationSafe("text.infoRestoreAll", "• <b>Restore All:</b> Restores all save files");
            AddTranslationSafe("text.infoGraveyard", "• <b>Restore Keeping Gravestones:</b> Restores but keeps the gravestones");
            AddTranslationSafe("text.creditsTitle", "<b>BackupSave - Credits</b>");
            AddTranslationSafe("text.creditsBasedOn", "<b>Based on:</b>");
            AddTranslationSafe("text.creditsSaveBackuper", "SaveBackuper by AnimeForevere");

            // ===== LOGS AND MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("log.restartingMenu", "[BackupSave] Restarting menu");
            AddTranslationSafe("log.prefixEnabled", "[BackupSave] Character name prefix enabled: ");

            // ===== MISSING TEXTS - ENGLISH ONLY =====
            AddTranslationSafe("text.reloadTip", "Restore, create, or delete updates the list immediately.");
            AddTranslationSafe("text.restorePointPlaceholder", "Type the name here...");
            AddTranslationSafe("text.restorePointInfo", "Restore points are permanent and will not be deleted by backup limits.");
            AddTranslationSafe("text.meshsaveInfo", "Delete the meshsave.txt file to restore the vehicle format.");
            AddTranslationSafe("text.importInfo", "Import your save from My Summer Car to My Winter Car with backup.");
            AddTranslationSafe("button.createRestorePoint", "Create Restore Point");
            AddTranslationSafe("button.deleteMeshsave", "<color=red>Delete meshsave.txt</color>");
            AddTranslationSafe("button.importSave", "Import Save from My Summer Car");

            // ===== ERROR MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("popup.failRestorePoint", "Failed to restore the restore point");
            AddTranslationSafe("popup.failRestoreBackup", "Failed to restore the backup");
            AddTranslationSafe("popup.failDeletePoint", "Failed to delete the restore point");
            AddTranslationSafe("popup.failDeleteBackup", "Failed to delete the backup");
            AddTranslationSafe("popup.failDeleteMeshsave", "Failed to delete meshsave.txt");
            AddTranslationSafe("popup.failCreatePoint", "Failed to create restore point");
            AddTranslationSafe("popup.errorImport", "Error importing save: ");
            
            // ===== LOG MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("log.creatingMWCBackup", "[BackupSave] Creating backup of current My Winter Car save...");
            AddTranslationSafe("log.saveImported", "[BackupSave] Save successfully imported from My Summer Car to My Winter Car!");

            // ===== IMPORT MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("popup.importSaveNotFound", "Error: No save found in My Summer Car!");
            AddTranslationSafe("popup.successImport", "Backup created successfully!\nSave from My Summer Car was imported to My Winter Car.");
            AddTranslationSafe("popup.successImportNoBackup", "Save from My Summer Car was imported to My Winter Car!\nWARNING: Failed to create backup of previous save.");
            AddTranslationSafe("popup.successImportNoSave", "Save from My Summer Car was imported to My Winter Car!\nWarning: No previous save to backup.");
            AddTranslationSafe("popup.warningBackupFailed", "WARNING: Failed to create backup of previous save.");
            AddTranslationSafe("popup.importPartial", "Some backups were imported, but a few failed.\nThe original folder was kept so you can try again.");
            AddTranslationSafe("popup.importFailed", "Failed to import backups.\nThe original folder was kept so you can try again.");

            // ===== AUTO RESTORE MESSAGES - ENGLISH ONLY =====
            AddTranslationSafe("popup.deathMessage", "You died!");
            AddTranslationSafe("popup.graveyardPreserved", "Your death history and gravestones have been preserved.");
            AddTranslationSafe("popup.failRestore", "Failed to restore your backup!\nCheck if you have a backup available.");

            // ===== BACKUP CREATION LOGS - ENGLISH ONLY =====
            AddTranslationSafe("log.backupCreated", "[BackupSave] Backup created successfully - Name: ");
            AddTranslationSafe("log.restorePointCreatedFailed", "Error creating restore point: No valid save file found!");
            AddTranslationSafe("log.backupRestored", "[BackupSave] Backup restored successfully: ");
            AddTranslationSafe("log.backupDeleted", "[BackupSave] Backup deleted: ");
            AddTranslationSafe("log.restorePointDeleted", "[BackupSave] Restore point deleted: ");
            AddTranslationSafe("log.oldBackupDeleted", "[BackupSave] Old backup deleted: ");
            AddTranslationSafe("log.backupLimitApplied", "[BackupSave] Backup limit applied. Keeping only the ");
            AddTranslationSafe("log.meshsaveDeleted", "[BackupSave] Meshsave.txt file successfully deleted!");
            AddTranslationSafe("log.errorCopyingFile", "Error copying file: ");
            AddTranslationSafe("log.errorCreatingBackup", "[BackupSave] Error creating backup: ");
            AddTranslationSafe("log.errorDeletingOldBackup", "[BackupSave] Error deleting old backup: ");
            AddTranslationSafe("log.errorRestoringBackup", "[BackupSave] Critical error restoring backup: ");
            AddTranslationSafe("log.errorDeletingItem", "[BackupSave] Error deleting item: ");
            AddTranslationSafe("log.errorDeletingMeshsave", "[BackupSave] Error deleting meshsave.txt: ");

            // ===== IMPORT LOGS - ENGLISH ONLY =====
            AddTranslationSafe("log.externalBackupImported", "[BackupSave] External backup imported: ");
            AddTranslationSafe("log.externalBackupFolderDeleted", "[BackupSave] External backup folder successfully deleted.");
            AddTranslationSafe("popup.importAlreadyDone", "Import has already been completed.\nThe backup list is already updated.");
            AddTranslationSafe("popup.importCompleted", "Total of ");
            AddTranslationSafe("popup.importCompletedEnd", " backup(s) imported successfully!\nBackup folder deleted.\nThe backup list has been updated.");
            AddTranslationSafe("label.importedPrefix", "IMPORTED - ");
            
            // ===== RESTORE POINT LOGS - ENGLISH ONLY =====
            AddTranslationSafe("log.restorePointCreated", "[BackupSave] Restore point created successfully - Name: ");
            AddTranslationSafe("log.errorCreatingRestorePoint", "[BackupSave] Error creating restore point");
            AddTranslationSafe("label.restorePointFolder", "Restoration Point");
        }
    }
}
