using System;
using System.Collections.Generic;
using System.Text;

namespace cursova2._0.Models
{
    public class SecurityItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public long FileSize { get; set; }
        public DateTime ScannedAt { get; set; } = DateTime.Now;

       
        public string Sha256Hash { get; set; }
        public SecurityLevel Level { get; set; } = SecurityLevel.Unknown;
        public string ThreatName { get; set; }
        public int DetectionCount { get; set; }   
        public int TotalEngines { get; set; }     

        public string RealFileType { get; set; }
        public bool IsTypeMismatch { get; set; }  

   
        public string ScanDetails { get; set; }
    }

    public enum SecurityLevel
    {
        Unknown,    
        Safe,      
        Suspicious,   
        Dangerous   
    }
}
