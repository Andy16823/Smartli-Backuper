using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backuper
{
    public enum Type{
        Directory,
        File
    }

    public class BackupSource
    {
        public String Name { get; set; }
        public String Path { get; set; }
        public Type Type { get; set; }
    }
}
