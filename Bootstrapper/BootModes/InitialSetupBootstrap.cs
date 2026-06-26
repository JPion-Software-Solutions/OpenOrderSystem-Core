using System;
using System.ComponentModel;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;

namespace OpenOrderSystem.Core.Bootstrapper.BootModes;

[BootModeUi(ShowInUi = false)]
public class InitialSetupBootstrap : IBootMode
{
    private WebApplicationBuilder? _bob;
    private WebApplication? _app;
    
    public string? RequestedFallback => throw new NotImplementedException();

    public WebApplicationBuilder? Bob => _bob;
    
    public WebApplication? App => _app;

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
