using Kippenkot.MailKit.Configuration;
using Kippenkot.MailKit.Interface;
using Kippenkot.MailKit.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MimeKit;
using RazorEngineCore;
using System.Text;

namespace Kippenkot.MailKit;

internal sealed class MailerService : IMailerService
{
    private readonly MailSettings _settings;

    public MailerService(IOptions<MailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<bool> SendAsync(MailData mailData, CancellationToken ct = default)
    {
        try
        {
            // Initialize a new instance of the MimeKit.MimeMessage class
            var mail = new MimeMessage();

            #region Sender / Receiver
            // Sender
            mail.From.Add(new MailboxAddress(_settings.DisplayName, mailData.From ?? _settings.From));
            mail.Sender = new MailboxAddress(mailData.DisplayName ?? _settings.DisplayName, mailData.From ?? _settings.From);

            // Receiver
            foreach (string mailAddress in mailData.To)
                mail.To.Add(MailboxAddress.Parse(mailAddress));

            // Set Reply to if specified in mail data
            if (!string.IsNullOrEmpty(mailData.ReplyTo))
                mail.ReplyTo.Add(new MailboxAddress(mailData.ReplyToName, mailData.ReplyTo));

            // BCC
            // Check if a BCC was supplied in the request
            if (mailData.Bcc != null)
            {
                // Get only addresses where value is not null or with whitespace. x = value of address
                foreach (string mailAddress in mailData.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
                    mail.Bcc.Add(MailboxAddress.Parse(mailAddress.Trim()));
            }

            // CC
            // Check if a CC address was supplied in the request
            if (mailData.Cc != null)
            {
                foreach (string mailAddress in mailData.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
                    mail.Cc.Add(MailboxAddress.Parse(mailAddress.Trim()));
            }
            #endregion

            #region Content

            // Add Content to Mime Message
            var body = new BodyBuilder();
            mail.Subject = mailData.Subject;
            body.HtmlBody = mailData.Body;
            mail.Body = body.ToMessageBody();

            #endregion

            #region Attachments

            if (mailData.Attachments != null)
            {
                byte[] attachmentFileByteArray;

                foreach (IFormFile attachment in mailData.Attachments)
                {
                    // Check if length of the file in bytes is larger than 0
                    if (attachment.Length > 0)
                    {
                        // Create a new memory stream and attach attachment to mail body
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            // Copy the attachment to the stream
                            attachment.CopyTo(memoryStream);
                            attachmentFileByteArray = memoryStream.ToArray();
                        }
                        // Add the attachment from the byte array
                        body.Attachments.Add(attachment.FileName, attachmentFileByteArray, ContentType.Parse(attachment.ContentType));
                    }
                }
            }

            #endregion

            #region Send Mail

            using var smtp = new SmtpClient();

            if (_settings.UseSSL)
            {
                await smtp.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.SslOnConnect, ct);
            }
            else if (_settings.UseStartTls)
            {
                await smtp.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, ct);
            }
            await smtp.AuthenticateAsync(_settings.UserName, _settings.Password, ct);
            await smtp.SendAsync(mail, ct);
            await smtp.DisconnectAsync(true, ct);

            #endregion

            return true;

        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<string> GetEmailTemplate<T>(string emailTemplate, T emailTemplateModel, CancellationToken ct, string? emailTemplatePath = null)
    {
        string mailTemplate = await LoadTemplateAsync(emailTemplate, ct, emailTemplatePath);

        IRazorEngine razorEngine = new RazorEngine();
        IRazorEngineCompiledTemplate modifiedMailTemplate = await razorEngine.CompileAsync(mailTemplate);

        return await modifiedMailTemplate.RunAsync(mailTemplate);
    }

    private async Task<string> LoadTemplateAsync(string emailTemplate, CancellationToken ct = default, string? emailTemplatePath = null)
    {
        if (emailTemplatePath is null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            emailTemplatePath = Path.Combine(baseDir, "MailTemplates");
        }

        string templatePath = Path.Combine(emailTemplatePath, $"{emailTemplate}.cshtml");

        using FileStream fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader sr = new StreamReader(fs, Encoding.Default);

        string mailTemplate = await sr.ReadToEndAsync().WaitAsync(ct).ConfigureAwait(false);
        sr.Close();

        return mailTemplate;
    }
}
