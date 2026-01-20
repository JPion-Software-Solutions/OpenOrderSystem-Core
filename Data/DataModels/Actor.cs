using System;
using Microsoft.AspNetCore.Identity;

namespace OpenOrderSystem.Core.Data.DataModels;

public class Actor
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public ActorType Type { get; set; } = ActorType.User;

    public string? UserId { get; set; }
    public IdentityUser? User { get; set; }

    public string? ActorScope { get; set; }
}

public enum ActorType
{
    User,

    Plugin,
    
    System
}
