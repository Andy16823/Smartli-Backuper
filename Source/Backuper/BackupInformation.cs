using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backuper
{
    public class BackupInformation
    {
        public string PlanName;
        public string PlanVersion;
        public BackupType BackupType;
        public string PreviousBackup;
        public DateTime PreviousBackupTime;
        public DateTime BackupTime;
        public List<String> FileMirror;

        public BackupInformation()
        {
            FileMirror = new List<String>();
        }

    }
}
