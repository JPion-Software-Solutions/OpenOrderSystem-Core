using System;
using System.ComponentModel;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;

namespace OpenOrderSystem.Core.Bootstrapper.BootModes;

[BootModeUi(ShowInUi = false)]
public class InitialSetupBootstrap : IBootMode
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
