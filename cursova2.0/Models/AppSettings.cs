using System;
using System.Collections.Generic;
using System.Text;

namespace MediaVault.Models
{
    public class AppSettings
    {
        public string LastMediaFolder { get; set; }
        public string TmdbApiKey { get; set; }
        public string Theme { get; set; } = "Dark";
    }
}
