using Kippenkot.MailKit.Models;

namespace Kippenkot.MailKit.Interface;

public interface IMailerService
{
    Task<bool> SendAsync(MailData mailData, CancellationToken ct);
    Task<string> GetEmailTemplate<T>(string emailTemplate, T emailTemplateModel, CancellationToken token, string? emailTemplatePath = null);
}
