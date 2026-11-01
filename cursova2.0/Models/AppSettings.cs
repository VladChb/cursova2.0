using System;
using System.Collections.Generic;
using System.Text;

namespace MediaVault.Models
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public bool AutoSave { get; set; } = true;
        public bool AutoScan { get; set; } = false;
        public bool ConfirmDelete { get; set; } = true;
    }
}
