using System;
using System.ComponentModel;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;

namespace OpenOrderSystem.Core.Bootstrapper.BootModes;

[BootModeUi(DisplayName = "Recovery Mode", Description = "Recover and repair an existing OOS install.")]
public class RecoveryBootstrap : IBootMode
{
    public string? RequestedFallback => throw new NotImplementedException();

    public WebApplication ConfigureMiddleware()
    {
        throw new NotImplementedException();
    }

    
    public IBootMode Initialize(string[] args, Configuration bootConfig)
    {
        return this;
    }
    public IBootMode LoadServices()
    {
        return this;
    }

    public bool PreflightCheck(string[] args, Configuration bootConfig, out string[] errors)
    {
        throw new NotImplementedException();
    }
}
