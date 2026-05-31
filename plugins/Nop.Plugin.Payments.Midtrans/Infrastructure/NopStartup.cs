using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Midtrans.Services;
using Nop.Web.Framework.Infrastructure.Extensions;

namespace Nop.Plugin.Payments.Midtrans.Infrastructure;

/// <summary>
/// Registers plugin services on application startup
/// </summary>
public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // typed HttpClient for Midtrans Snap API calls (honors nopCommerce proxy settings)
        services.AddHttpClient<MidtransService>().WithProxy();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 1;
}
