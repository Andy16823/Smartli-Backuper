using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Backuper
{
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
        public static void CreateBackupAsync(BackupPlan plan, String location, Action<object[]> callback, object[] args = null)
        {
            Task.Run(() =>
            {
                CreateBackup(plan, location);
                if (callback != null)
                {
                    callback(args);
                }
            });
        }

        /// <summary>
        /// Create a backup based on a backup plan.
        /// </summary>
        /// <param name="plan">The backup plan to execute.</param>
        /// <param name="location">The location where the backup will be stored.</param>
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
        public static void ExtractBackupAsync(BackupPlan plan, String backupName, String location, String destination, Action callback)
        {
            Task.Run(() =>
            {
                ExtractBackup(plan, backupName, location, destination);
                if(callback != null)
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
        public static void ExtractBackup(BackupPlan plan, String backupName, String location, String destination)
        {
            var backupFile = Path.Combine(GetPlanFolder(plan, location), backupName);
            System.IO.Compression.ZipFile.ExtractToDirectory(backupFile, destination);
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
                RestoreBackup(backupsource);
                if (callback != null)
                {
                    callback();
                }
            });
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
                        if(Directory.Exists(source.Path))
                        {
                            DeleteDirectory(source.Path);
                        }
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
    }
}
