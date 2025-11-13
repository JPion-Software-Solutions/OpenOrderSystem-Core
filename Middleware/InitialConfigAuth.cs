
using Microsoft.AspNetCore.Http.Features;
using OpenOrderSystem.Core.Attributes;

namespace OpenOrderSystem.Core.Middleware
{
    public class InitialConfigAuth : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var endpoint = context.Features.Get<IEndpointFeature>()?.Endpoint;
            var intialConfig = endpoint?.Metadata.GetMetadata<InitialConfigAttribute>();
            if (intialConfig == null)
            {
                await next(context);
                return;
            }

            //get the initial configuration status (look for config file).
            var configured = File.Exists(Path.Combine("config", "siteSettings.json"));

            if (intialConfig.InvertCondition)
                configured = !configured;

            //redirect if system is already configured
            if (configured)
            {
                context.Response.Redirect(intialConfig.RedirectOnFail);
            }

            //allow request to go through
            await next(context);
            return;

        }
    }
}
