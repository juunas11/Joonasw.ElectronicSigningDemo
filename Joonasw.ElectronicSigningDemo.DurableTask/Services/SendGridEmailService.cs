using SendGrid;
using SendGrid.Helpers.Mail;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Services;

public class SendGridEmailService(
    SendGridClient client,
    string fromEmail)
{
    public async Task SendEmailAsync(string toEmail, string subject, string plainTextContent)
    {
        var message = new SendGridMessage();
        message.SetFrom(fromEmail);

        message.AddTo(toEmail);
        message.SetSubject(subject);
        message.AddContent("text/plain", plainTextContent);

        await client.SendEmailAsync(message);
    }
}
