using System;

namespace OpenOrderSystem.Core.Bootstrapper.Interfaces;

public interface IBootMode
{
    public string? RequestedFallback { get; }

    public bool PreflightCheck(string[] args, Configuration bootConfig, out string[] errors);

    public IBootMode Initialize(string[] args, Configuration bootConfig);

    public IBootMode LoadServices();

    public WebApplication ConfigureMiddleware();
}
