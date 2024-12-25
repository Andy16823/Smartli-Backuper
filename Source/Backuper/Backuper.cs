using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Backuper
{
    /// <summary>
    /// The type from the backup
    /// </summary>
    public enum BackupType
    {
        Full = 0,
        Incremental = 1
    }

    /// <summary>
    /// Class providing methods for backup plan serialization, deserialization, and creation.
    /// </summary>
    public class Backuper
    {
        public delegate void BackupEventHandler(BackupPlan plan, object[] args);

        /// <summary>
        /// Serialize a collection of backup plans to a JSON file.
        /// </summary>
        /// <param name="plans">The list of backup plans to serialize.</param>
        /// <param name="file">The path to the output JSON file.</param>
        public static void SerializePlans(List<BackupPlan> plans, String file)
        {
            var json = JsonConvert.SerializeObject(plans);
            System.IO.File.WriteAllText(file, json);
        }

        /// <summary>
        /// Serialize a collection of backup plans to a JSON file.
        /// </summary>
        /// <param name="plans">The collection of backup plans to serialize.</param>
        /// <param name="file">The path to the output JSON file.</param>
        public static void SerializePlans(ObservableCollection<BackupPlan> plans, String file)
        {
            SerializePlans(plans.ToList(), file);
        }

        /// <summary>
        /// Deserialize a collection of backup plans from a JSON file.
        /// </summary>
        /// <param name="file">The path to the JSON file containing backup plans.</param>
        /// <returns>The deserialized list of backup plans.</returns>
        public static List<BackupPlan> DeserializePlans(String file)
        {
            var json = System.IO.File.ReadAllText(file);
            return JsonConvert.DeserializeObject<List<BackupPlan>>(json);
        }

        /// <summary>
        /// Deserialize a collection of backup plans from a JSON file.
        /// </summary>
        /// <param name="file">The path to the JSON file containing backup plans.</param>
        /// <returns>The deserialized collection of backup plans.</returns>
        public static ObservableCollection<BackupPlan> DeserializeObservableCollection(String file)
        {
            return new ObservableCollection<BackupPlan>(DeserializePlans(file));
        }

        /// <summary>
        /// Serialize a single backup plan to JSON.
        /// </summary>
        /// <param name="plan">The backup plan to serialize.</param>
        /// <returns>The JSON representation of the backup plan.</returns>
        public static String SerializePlan(BackupPlan plan)
        {
            return JsonConvert.SerializeObject(plan);
        }

        public static BackupPlan DeserializePlan(String planJson)
        {
            return JsonConvert.DeserializeObject<BackupPlan>(planJson);
        }

        /// <summary>
        /// Asynchronously create a backup based on a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan to execute.</param>
        /// <param name="location">The location where the backup will be stored.</param>
        /// <param name="callback">Optional callback to execute after backup creation.</param>
        public static void CreateBackupAsync(BackupPlan plan, String location, BackupType backupType, Action<object[], bool> callback, object[] args = null)
        {
            Task.Run(() =>
            {
                var result = CreateBackupWithZipArchive(plan, location, backupType);
                if (callback != null)
                {
                    callback(args, result);
                }
            });
        }

        /// <summary>
        /// Create a backup based on a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan to execute.</param>
        /// <param name="location">The location where the backup will be stored.</param>
        [Obsolete("CreateBackup is deprecated, please use CreateBackupAsync instead.")]
        public static void CreateBackup(BackupPlan plan, String location)
        {
            // Create an plan directory
            String planDir = Path.Combine(location, plan.Name);

            // Create an name for the backup
            String backupPlainName = plan.Name + "_" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString();
            String backupName = CalculateMD5Hash(backupPlainName);

            // Create the backup folder if he dont exist
            String backupDir = Path.Combine(planDir, backupName);
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            // Copy folders and files to new destination
            foreach (var item in plan.Sources)
            {
                if (item.Type == Type.Directory)
                {
                    if (Directory.Exists(item.Path))
                    {
                        var directoryInfo = new DirectoryInfo(item.Path);
                        var directory = Path.Combine(backupDir, directoryInfo.Name);
                        CopyDirectory(item.Path, directory);
                    }
                }
                else if (item.Type == Type.File)
                {
                    if (File.Exists(item.Path))
                    {
                        var fileInfo = new FileInfo(item.Path);
                        var fileName = Path.Combine(backupDir, fileInfo.Name);
                        File.Copy(item.Path, fileName);
                    }
                }
            }
            var planJson = SerializePlan(plan);
            File.WriteAllText(Path.Combine(backupDir, "plan.json"), planJson);

            // zip folder
            var zipFile = Path.Combine(planDir, backupName + ".smlb");
            System.IO.Compression.ZipFile.CreateFromDirectory(backupDir, zipFile);
            DeleteDirectory(backupDir);

            plan.LastBackup = DateTime.Now;
            plan.LastBackupName = backupName;
        }

        /// <summary>
        /// Delete a directory and all its contents recursively.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public static void DeleteDirectory(String path)
        {
            String[] files = Directory.GetFiles(path);
            String[] dirs = Directory.GetDirectories(path);

            foreach (var file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in dirs)
            {
                DeleteDirectory(dir);
            }
            try
            {
                Directory.Delete(path, false);
            }
            catch
            {
                
            }
        }

        /// <summary>
        /// Delete a directory and all its contents recursively.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public static void CopyDirectory(string path, string destination)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            FileInfo[] files = directoryInfo.GetFiles();
            foreach (var item in files)
            {
                String destFile = Path.Combine(destination, item.Name);
                File.Copy(item.FullName, destFile, true);
            }

            DirectoryInfo[] dirs = directoryInfo.GetDirectories();
            foreach (DirectoryInfo directory in dirs)
            {
                String destDir = Path.Combine(destination, directory.Name);
                CopyDirectory(directory.FullName, destDir);
            }
        }

        /// <summary>
        /// Calculate the MD5 hash of the input string.
        /// </summary>
        /// <param name="input">The input string to hash.</param>
        /// <returns>The MD5 hash of the input string.</returns>
        public static string CalculateMD5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                // Konvertiere den Eingabestring in ein Byte-Array
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);

                // Berechne den MD5-Hash des Byte-Arrays
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Konvertiere den MD5-Hash in einen hexadezimalen String
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Delete the directory associated with a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan whose directory will be deleted.</param>
        /// <param name="location">The base location of backup plans.</param>
        public static void DeleteBackupDirectory(BackupPlan plan, String location)
        {
            var directory = GetPlanFolder(plan, location);
            if(Directory.Exists(directory))
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>
        /// Get the folder associated with a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan whose folder will be retrieved.</param>
        /// <param name="location">The base location of backup plans.</param>
        /// <returns>The folder path of the specified backup plan.</returns>
        public static String GetPlanFolder(BackupPlan plan, String location)
        {
            return Path.Combine(location, plan.Name);
        }

        /// <summary>
        /// Get a list of backup files associated with a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan whose backups are to be retrieved.</param>
        /// <param name="location">The base location of backup plans.</param>
        /// <returns>A list of backup file names.</returns>
        public static List<String> GetBackups(BackupPlan plan, String location)
        {
            var result = new List<String>();
            var path = GetPlanFolder(plan, location);
            if(Directory.Exists(path))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                FileInfo[] files = directoryInfo.GetFiles().OrderBy(p => p.CreationTimeUtc).ToArray();
                foreach(var file in files) 
                {
                    if(file.Extension.Equals(".smlb"))
                    {
                        result.Add(file.Name);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get an observable collection of backup files associated with a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan whose backups are to be retrieved.</param>
        /// <param name="location">The base location of backup plans.</param>
        /// <returns>An observable collection of backup file names.</returns>
        public static ObservableCollection<String> GetBackupsObservableCollection(BackupPlan plan, String location)
        {
            return new ObservableCollection<string>(GetBackups(plan, location));
        }

        /// <summary>
        /// Asynchronously extract a backup associated with a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan associated with the backup to extract.</param>
        /// <param name="backupName">The name of the backup to extract.</param>
        /// <param name="location">The base location of backup plans.</param>
        /// <param name="destination">The destination where the backup will be extracted.</param>
        /// <param name="callback">Optional callback to execute after extraction.</param>
        public static void ExtractBackupAsync(String backupArchive, String destination, Action callback)
        {
            Task.Run(() =>
            {
                var restoreChain = BuildRestoreChain(backupArchive);
                for (int i = restoreChain.Count - 1; i >= 0; i--)
                {
                    var archive = restoreChain[i];
                    ExtractBackup(archive, destination);
                    Debug.WriteLine($"Extracted archive {i}");
                }
                VerifyMirror(backupArchive, destination);

                if (callback != null)
                {
                    callback();
                }
            });
        }

        /// <summary>
        /// Extract a backup associated with a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan associated with the backup to extract.</param>
        /// <param name="backupName">The name of the backup to extract.</param>
        /// <param name="location">The base location of backup plans.</param>
        /// <param name="destination">The destination where the backup will be extracted.</param>
        public static void ExtractBackup(String backupArchive, String destination)
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(backupArchive, destination, true);
        }

        /// <summary>
        /// Verifys the 
        /// </summary>
        /// <param name="archiveFile"></param>
        /// <param name="targetDir"></param>
        internal static void VerifyMirror(String archiveFile, String targetDir)
        {
            var backupFile = GetFileFromArchive(archiveFile, "archive.backup");
            var backupInformation = JsonConvert.DeserializeObject<BackupInformation>(backupFile);

            var files = Directory.GetFiles(targetDir);

            foreach (var file in files)
            {
                SyncFileWithMirror(file, String.Empty, backupInformation.FileMirror);
            }

            var directories = Directory.GetDirectories(targetDir);
            foreach(var directory in directories)
            {
                SyncDirectoryWithMirror(directory, String.Empty, backupInformation.FileMirror);
            }

        }

        /// <summary>
        /// Checks if the given mirrior contains the directory
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="startDir"></param>
        /// <param name="mirror"></param>
        internal static void SyncDirectoryWithMirror(String directory, String startDir, List<String> mirror)
        {
            var dirInfo = new DirectoryInfo(directory);
            var dirName = dirInfo.Name;
            var relativeDirectoryName = Path.Combine(startDir, dirName);
            if(!mirror.Contains(relativeDirectoryName))
            {
                System.IO.Directory.Delete(directory, true);
                return;
            }

            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                SyncFileWithMirror(file, Path.Combine(startDir, dirName), mirror);
            }

            var directories = Directory.GetDirectories(directory);
            foreach (var dir in directories)
            {
                SyncDirectoryWithMirror(dir, Path.Combine(startDir, dirName), mirror);
            }
        }

        /// <summary>
        /// Checks if the given mirror contains the file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="targetDir"></param>
        /// <param name="mirror"></param>
        internal static void SyncFileWithMirror(String file, String targetDir, List<String> mirror)
        {
            var fileName = Path.GetFileName(file);
            var relativeFileName = Path.Combine(targetDir, fileName);
            if(!mirror.Contains(relativeFileName))
            {
                System.IO.File.Delete(file);
            }
        }

        /// <summary>
        /// Asynchronously restores a backup from the specified source.
        /// </summary>
        /// <param name="backupSource">The path to the backup source.</param>
        /// <param name="callback">Optional callback to execute after restoration.</param>
        public static void RestoreBackupAsync(String backupsource, Action callback)
        {
            Task.Run(() =>
            {
                var restoreChain = BuildRestoreChain(backupsource);
                for (int i = restoreChain.Count - 1; i >= 0; i--)
                {
                    var archive = restoreChain[i];
                    RestoreBackup(archive);
                    Debug.WriteLine($"Extracted archive {i}");
                }
                VerifyBackupIntegrity(backupsource);

                if (callback != null)
                {
                    callback();
                }
            });
        }

        /// <summary>
        /// Checks the backup integrity and deletes files and folders wich was not existing
        /// at the time of the backup
        /// </summary>
        /// <param name="archivePath"></param>
        internal static void VerifyBackupIntegrity(String archivePath)
        {
            var backupFile = GetFileFromArchive(archivePath, "archive.backup");
            var backupInformation = JsonConvert.DeserializeObject<BackupInformation>(backupFile);

            var planFile = GetFileFromArchive(archivePath, "plan.json");
            var plan = JsonConvert.DeserializeObject<BackupPlan>(planFile);

            foreach (var source in plan.Sources)
            {
                if (source.Type == Type.Directory)
                {
                    if (Directory.Exists(source.Path))
                    {
                        SyncDirectoryWithMirror(source.Path, "", backupInformation.FileMirror);
                    }
                }
                else if (source.Type == Type.File)
                {
                    if (File.Exists(source.Path))
                    {
                        SyncFileWithMirror(source.Path, "", backupInformation.FileMirror);
                    }
                }
            }
        }

        /// <summary>
        /// Restores a backup from the specified source.
        /// </summary>
        /// <param name="backupSource">The path to the backup source.</param>
        public static void RestoreBackup(String backupSource)
        {
            // Setup the working dir
            FileInfo fileInfo = new FileInfo(backupSource);
            String workingDir = Path.Combine(fileInfo.DirectoryName, "tmp");

            if (Directory.Exists(workingDir))
            {
                Directory.Delete(workingDir, true);
            }

            // Extract the archive into the working directory
            System.IO.Compression.ZipFile.ExtractToDirectory(backupSource, workingDir);

            // Check if an plan.json exist
            var planFile = Path.Combine(workingDir, "plan.json");
            if (!File.Exists(planFile))
            {
                Directory.Delete(workingDir, true);
                return;
            }

            var planJson = File.ReadAllText(planFile);
            var plan = DeserializePlan(planJson);

            // Move files
            foreach (var source in plan.Sources)
            {
                var sourcefile = Path.Combine(workingDir, source.Name);
                if(source.Type == Type.Directory)
                {
                    if (Directory.Exists(sourcefile))
                    {
                        CopyDirectory(sourcefile, source.Path);
                    }
                }
                else if(source.Type == Type.File)
                {
                    if(File.Exists(sourcefile))
                    {
                        File.Move(sourcefile, source.Path, true);
                    }
                }
            }

            // Delete the working dir
            Directory.Delete(workingDir, true);
        }

        private static int GetDaysFromSchedule(Schedule schedule)
        {
            switch (schedule)
            {
                case Schedule.Daily:
                    return 1;
                case Schedule.TwoDays:
                    return 2;
                case Schedule.ThreeDays:
                    return 3;
                case Schedule.FourDays:
                    return 4;
                case Schedule.FiveDays:
                    return 5;
                case Schedule.SixDays:
                    return 6;
                case Schedule.SevenDays:
                    return 7;
                default:
                    return 1;
            }
        }

        public static bool IsBackupRequired(BackupPlan plan)
        {
            var now = DateTime.Now;
            var nextBackup = plan.LastBackup.AddDays(GetDaysFromSchedule(plan.Schedule));
            if(now >= nextBackup)
            {
                return true;
            }
            return false;
        }

        public static void CheckForDueBackups(List<BackupPlan> backupPlans, BackupEventHandler callback, object[] args = null)
        {
            Parallel.ForEach(backupPlans, plan =>
            {
                plan.BackupRequired = IsBackupRequired(plan);
                callback(plan, args);
            });
        }

        public static void CheckForDueBackups(ObservableCollection<BackupPlan> backupPlans, BackupEventHandler callback, object[] args = null)
        {
            CheckForDueBackups(backupPlans.ToList(), callback, args);
        }

        public static List<BackupPlan> GetExpiredPlans(List<BackupPlan> backupPlans) 
        {
            var expiredPlans = new List<BackupPlan>();
            Parallel.ForEach(backupPlans, plan =>
            {
                if(plan.BackupRequired)
                {
                    expiredPlans.Add(plan);
                }
            });
            return expiredPlans;
        }

        public static void ExportBackupAsync(BackupPlan plan, String backupsLocation, String destination, Action<object[]> callback, object[] args = null)
        {
            Task.Run(() =>
            {
                ExportBackup(plan, backupsLocation, destination);
                if(callback != null)
                {
                    callback(args);
                }
            });
        }

        public static void ExportBackupSecuredAsynch(BackupPlan plan, String backupsLocation, String destination, String password, Action<object[]> callback, object[] args = null)
        {
            Task.Run(() =>
            {
                // Export the backup
                ExportBackup(plan, backupsLocation, destination);

                // Encrypt the file
                var fileInfo = new FileInfo(destination); 
                var encryptedFile = fileInfo.DirectoryName + "/enc_" + fileInfo.Name;
                Cryptography.EncryptFile(destination, encryptedFile, password);
                File.Delete(destination);

                // Callback
                if (callback != null)
                {
                    callback(args);
                }
            });
        }

        public static void ExportBackup(BackupPlan plan, String backupsLocation, String destionationFile)
        {
            var planDirectory = GetPlanFolder(plan, backupsLocation);
            var destinationInfo = new FileInfo(destionationFile);
            var destination = destinationInfo.DirectoryName;

            // Create an temp working directory
            var tmpDirectory = Path.Combine(destination, "_tmp_");
            if(Directory.Exists(tmpDirectory))
            {
                DeleteDirectory(tmpDirectory);
            }
            Directory.CreateDirectory(tmpDirectory);

            // Save the plan in the working directory
            var planFile = Path.Combine(tmpDirectory, "plan.json");
            var planJson = SerializePlan(plan);
            File.WriteAllText(planFile, planJson);

            // Copy the files
            CopyDirectory(planDirectory, tmpDirectory);

            // Zip the tmp directory
            System.IO.Compression.ZipFile.CreateFromDirectory(tmpDirectory, destionationFile);
            Directory.Delete(tmpDirectory, true);
        }

        public static void ImportFromArchiveAsync(String archiveFile, String backupsPath, BackupEventHandler callback, object[] args = null)
        {
            Task.Run(() =>
            {
                var plan = ImportFromArchive(archiveFile, backupsPath);
                Console.WriteLine("Export Done");
                callback(plan, args);
            });
        }

        public static void ImportFromArchiveSecuredAsync(String archiveFile, String backupsPath, String password, BackupEventHandler callback, object[] args = null)
        {
            Task.Run(() =>
            {
                var fileInfo = new FileInfo(archiveFile);
                String outputFile = Path.Combine(fileInfo.DirectoryName, "dec_" + fileInfo.Name);
                var result = Cryptography.DecryptFile(archiveFile, outputFile, password);
                if(!result)
                {
                    File.Delete(outputFile);
                    callback(null, args);
                    return;
                }
                var plan = ImportFromArchive(outputFile, backupsPath);
                File.Delete(outputFile);
                callback(plan, args);
            });
        }

        public static BackupPlan ImportFromArchive(String archiveFile, String backupsPath)
        {
            var guid = Guid.NewGuid(); 

            // Create an working directory
            var tmpDirectory = Path.Combine(backupsPath, guid.ToString());
            if(Directory.Exists(tmpDirectory))
            {
                DeleteDirectory(tmpDirectory);
            }
            Directory.CreateDirectory(tmpDirectory);

            // Extract the zip
            System.IO.Compression.ZipFile.ExtractToDirectory(archiveFile, tmpDirectory);

            // Check if a plan exist
            var planFile = Path.Combine(tmpDirectory, "plan.json");
            if(!File.Exists(planFile))
            {
                DeleteDirectory(tmpDirectory);
                return null;
            }

            // Create an new plan and an directory for the plan
            var plan = DeserializePlan(File.ReadAllText(planFile));
            File.Delete(planFile);

            var planDirectory = Path.Combine(backupsPath, plan.Name);
            if (Directory.Exists(planDirectory))
            {
                DeleteDirectory(tmpDirectory);
                return null;
            }
            CopyDirectory(tmpDirectory, planDirectory);

            // Delete the tmp directory
            DeleteDirectory(tmpDirectory);
            return plan;
        }


        public static bool CreateBackupWithZipArchive(BackupPlan plan, String location, BackupType backupType)
        {
            // Create an plan directory
            var planDir = Path.Combine(location, plan.Name);
            if(!Directory.Exists(planDir))
            {
                Directory.CreateDirectory(planDir);
            }

            // Create an name for the backup
            String backupPlainName = plan.Name + "_" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString();
            String backupName = CalculateMD5Hash(backupPlainName);
            var archiveName = Path.Combine(planDir, backupName + ".smlb");

            // Create the backup information
            BackupInformation backupInformation = new BackupInformation()
            {
                PlanName = backupName,
                PlanVersion = "1.0.1",
                PreviousBackup = plan.LastBackupName,
                PreviousBackupTime = plan.LastBackup,
                BackupTime = DateTime.Now,
                BackupType = backupType
            };

            // Check if archive exist
            if (File.Exists(archiveName))
            {
                File.Delete(archiveName);
            }

            // Create Zip
            if(!CreateArchive(plan, archiveName, backupInformation))
            {
                File.Delete(archiveName);
                return false;
            }

            plan.LastBackup = DateTime.Now;
            plan.LastBackupName = backupName;
            return true;
        }

        public static bool CreateArchive(BackupPlan plan, string archiveName, BackupInformation backupInformation)
        {
            using (var destinationStream = new FileStream(archiveName, FileMode.Create))
            {
                using (var zipArchiv = new ZipArchive(destinationStream, ZipArchiveMode.Create))
                {
                    // Add folders and files to the zip
                    foreach (var item in plan.Sources)
                    {
                        // Add folders to the archive
                        if (item.Type == Type.Directory)
                        {
                            if (Directory.Exists(item.Path))
                            {
                                var result = AddFolderToZip(zipArchiv, item.Path, item.Name, "", ref backupInformation);
                                if(result == false)
                                {
                                    return false;
                                }
                            }
                        }
                        // Add Files to the archive
                        else if (item.Type == Type.File)
                        {
                            if (File.Exists(item.Path))
                            {
                                var result = AddFileToZipArchive(zipArchiv, item.Path, item.Name, ref backupInformation);
                                if(result == false)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    // Add the plan to the archive
                    var planJson = SerializePlan(plan);
                    CreateFileInArchive(zipArchiv, "plan.json", planJson);

                    var backupInformationJson = JsonConvert.SerializeObject(backupInformation);
                    CreateFileInArchive(zipArchiv, "archive.backup", backupInformationJson);
                }
            }
            return true;
        }

        public static bool AddFolderToZip(ZipArchive archive, string folderPath, string folderName, string parentFolderName, ref BackupInformation backupInformation)
        {
            //var folderName = Path.GetFileName(folderPath);
            var folderArchiveName = Path.Combine(parentFolderName, folderName);
            backupInformation.FileMirror.Add(folderArchiveName);

            foreach (var file in Directory.GetFiles(folderPath))
            {
                var entryName = Path.Combine(folderArchiveName, Path.GetFileName(file));
                var copyResult = AddFileToZipArchive(archive, file, entryName, ref backupInformation);
                if (copyResult == false)
                {
                    return false;
                }
            }

            foreach (var subFolder in Directory.GetDirectories(folderPath))
            {
                var copyResult = AddFolderToZip(archive, subFolder, Path.GetFileName(subFolder), folderArchiveName, ref backupInformation);
                if(copyResult == false)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool AddFileToZipArchive(ZipArchive archive, string filePath, string entryName, ref BackupInformation backupInformation)
        {
            backupInformation.FileMirror.Add(entryName);

            if (IncludeFile(filePath, backupInformation))
            {
                var entry = archive.CreateEntry(entryName);
                try
                {
                    using (var sourceStream = new FileStream(filePath, FileMode.Open))
                    {
                        using (Stream entryStream = entry.Open())
                        {
                            sourceStream.CopyTo(entryStream);
                        }
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }

        public static void CreateFileInArchive(ZipArchive archive, String name, String content)
        {
            var entry = archive.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(content);
            }
        }

        public static bool IncludeFile(String file, BackupInformation backupInformation)
        {
            if(backupInformation.BackupType == BackupType.Incremental)
            {
                var fileInfo = new FileInfo(file);
                return fileInfo.LastWriteTime > backupInformation.PreviousBackupTime;
            }
            return true;
        }

        public static BackupInformation GetBackupInformationFromArchive(string archiveFile)
        {
            var backupInfoStr = GetFileFromArchive(archiveFile, "archive.backup");
            return JsonConvert.DeserializeObject<BackupInformation>(backupInfoStr);
        }

        public static String GetFileFromArchive(string archiveFile, String fileName)
        {
            using (ZipArchive archive = ZipFile.OpenRead(archiveFile))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        using (StreamReader reader = new StreamReader(entry.Open()))
                        {
                            string content = reader.ReadToEnd();
                            return content;

                        }
                    }
                }
            }
            return String.Empty;
        }

        public static bool CanBackupGetRestored(String archiveFile)
        {
            var backupFile = GetFileFromArchive(archiveFile, "archive.backup");
            var planFile = GetFileFromArchive(archiveFile, "plan.json");
            if (!String.IsNullOrEmpty(backupFile) && !String.IsNullOrEmpty(planFile))
            {
                var backupInformation = JsonConvert.DeserializeObject<BackupInformation>(backupFile);
                if(backupInformation.BackupType == BackupType.Incremental)
                {
                    var basepath = Path.GetDirectoryName(archiveFile);
                    var dependencyFile = Path.Combine(basepath, backupInformation.PreviousBackup + ".smlb");
                    if(File.Exists(dependencyFile))
                    {
                        return CanBackupGetRestored(dependencyFile);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Builds the Restore chain for a given backup archive.
        /// </summary>
        /// <param name="archiveFile"></param>
        /// <returns></returns>
        public static List<String> BuildRestoreChain(String archiveFile)
        {
            var basepath = Path.GetDirectoryName(archiveFile);
            List<String> result = new List<String>();
            if(CanBackupGetRestored(archiveFile))
            {
                result.Add(archiveFile);

                var backupFile = GetFileFromArchive(archiveFile, "archive.backup");
                var backupInformation = JsonConvert.DeserializeObject<BackupInformation>(backupFile);
                if(backupInformation.BackupType == BackupType.Incremental)
                {
                    var dependencyFile = Path.Combine(basepath, backupInformation.PreviousBackup + ".smlb");
                    GetDependency(dependencyFile, basepath, ref result);
                }
            }
            return result;
        }

        public static void GetDependency(String archiveFile, String basepath, ref List<String> dependencys)
        {
            dependencys.Add(archiveFile);
            var backupFile = GetFileFromArchive(archiveFile, "archive.backup");
            var backupInformation = JsonConvert.DeserializeObject<BackupInformation>(backupFile);

            if(backupInformation.BackupType == BackupType.Incremental)
            {
                var dependencyFile = Path.Combine(basepath, backupInformation.PreviousBackup + ".smlb");
                GetDependency(dependencyFile, basepath, ref dependencys);
            }
        }

        public static bool ContainsSource(BackupPlan backupPlan, String name)
        {
            foreach (var source in backupPlan.Sources)
            {
                if(source.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

    }
}
