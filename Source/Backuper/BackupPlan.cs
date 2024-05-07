using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backuper
{
    public enum Schedule
    {
        [Description("24 Hours")]
        Daily,
        [Description("2 Days")]
        TwoDays,
        [Description("3 Days")]
        ThreeDays,
        [Description("4 Days")]
        FourDays,
        [Description("5 Days")]
        FiveDays,
        [Description("6 Days")]
        SixDays,
        [Description("7 Days")]
        SevenDays,
    }

    public class BackupPlan
    {
        public String Name { get; set; }
        public Schedule Schedule { get; set; }
        public DateTime LastBackup { get; set; }
        public String LastBackupName { get; set; }
        public List<BackupSource> Sources { get; set; }
        public bool BackupRequired { get; set; } = false; // Value gets updated from CheckForDueBackups

        public BackupPlan()
        {
            Sources = new List<BackupSource>();
        }

        public BackupPlan(String Name, Schedule schedule)
        {
            Sources = new List<BackupSource>();
            this.Name = Name;
            Schedule = schedule;
        }
    }
}
