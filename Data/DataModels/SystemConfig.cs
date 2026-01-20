using System;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Data.DataModels;

public class SystemConfig
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsLocked { get; set; }

    public string ActorId { get; set; } = string.Empty;
    public Actor? Actor { get; set; }                                                   
}
