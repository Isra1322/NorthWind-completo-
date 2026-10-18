using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NorthWind.Entities.Interfaces;
using NorthWind.Sales.Backend.SmtpGateways.Options;

namespace NorthWind.Sales.Backend.SmtpGateways;

internal class MailService(IOptions<SmtpOptions> smtpOptions,
    ILogger<MailService> logger) : IMailService
{
    public async Task SendMailToAdministrator(string subject, string body)
    {
        try
        {
            using MailMessage message = new(
                smtpOptions.Value.SenderEmail, smtpOptions.Value.AdministratorEmail)
            {
                Subject = subject,
                Body = body
            };
            using SmtpClient client = new(
                smtpOptions.Value.SmtpHost, smtpOptions.Value.SmtpHostPort)
            {
                Credentials = new NetworkCredential(
                    smtpOptions.Value.SmtpUserName, smtpOptions.Value.SmtpPassword),
                EnableSsl = true
            };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.SendMailAsync(message, timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            throw;
        }
    }
}
