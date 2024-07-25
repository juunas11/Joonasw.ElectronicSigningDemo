using SendGrid;
using SendGrid.Helpers.Mail;

namespace Joonasw.ElectronicSigningDemo.Workflows;

public class SendGridEmailService
{
    private readonly SendGridClient _client;
    private readonly string _fromEmail;

    public SendGridEmailService(
        SendGridClient client,
        string fromEmail)
    {
        _client = client;
        _fromEmail = fromEmail;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string plainTextContent)
    {
        var message = new SendGridMessage();
        message.SetFrom(_fromEmail);

        message.AddTo(toEmail);
        message.SetSubject(subject);
        message.AddContent("text/plain", plainTextContent);

        await _client.SendEmailAsync(message);
    }
}
