using System;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Core;

public class SystemConfig
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsLocked { get; set; }                                                
}
