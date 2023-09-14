using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Kippenkot.MailKit.Interface;

namespace Kippenkot.MailKit.Configuration;

public static class DependencyInjection
{
    public static void AddMailkit(this WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<IMailerService, MailerService>();
        builder.Services.Configure<MailSettings>(builder.Configuration.GetSection(nameof(MailSettings)));
    }

    public static void AddMailkit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IMailerService, MailerService>();
        services.Configure<MailSettings>(configuration.GetSection(nameof(MailSettings)));
    }
}
