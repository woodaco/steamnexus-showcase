using Microsoft.AspNetCore.Identity.UI.Services;
using SendGrid;
using SendGrid.Helpers.Mail;
using Newtonsoft.Json;
using SteamProject.Helpers;

namespace SteamProject.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    private static class EmailSubjects
    {
        public const string Confirmation = "Email Confirmation";
        public const string Invitation = "Invitation";
        public const string Reset = "Password Reset";
    }

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var sendGridKey = _configuration["SendGridKey"];
        if (string.IsNullOrEmpty(sendGridKey))
        {
            _logger.LogWarning("SendGrid API key not configured. Email to {Email} with subject '{Subject}' was not sent", email, subject);
            return Task.CompletedTask;
        }
        return Execute(sendGridKey, subject, htmlMessage, email);
    }

    public Task Execute(string apiKey, string subject, string message, string email)
    {
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@steamnexus.com";
        var fromName = _configuration["Email:FromName"] ?? "SteamNexus";
        var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7298";

        var client = new SendGridClient(apiKey);
        var msg = new SendGridMessage
        {
            From = new EmailAddress(fromAddress, fromName),
            Subject = subject,
            PlainTextContent = message,
            HtmlContent = message,
        };

        msg.AddTo(new EmailAddress(email));
        msg.SetClickTracking(false, false);

        var (templateId, templateData) = GetTemplateInfo(subject, message, baseUrl);
        if (!string.IsNullOrEmpty(templateId))
        {
            msg.TemplateId = templateId;
            msg.SetTemplateData(templateData);
        }

        _logger.LogInformation("Sending email to {Email} with subject '{Subject}'", email, subject);
        return client.SendEmailAsync(msg);
    }

    private (string? templateId, object? templateData) GetTemplateInfo(string subject, string message, string baseUrl)
    {
        return subject switch
        {
            EmailSubjects.Confirmation => (
                _configuration["SendGrid:ConfirmTemplateId"],
                new EmailTemplateData { Url = HelperMethods.FixEmailUrl(message) }
            ),
            EmailSubjects.Invitation => (
                _configuration["SendGrid:InviteTemplateId"],
                new EmailTemplateData { Url = baseUrl }
            ),
            _ => (
                _configuration["SendGrid:ResetTemplateId"],
                new EmailTemplateData { Url = HelperMethods.FixResetUrl(message) }
            )
        };
    }

    private class EmailTemplateData
    {
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;
    }
}