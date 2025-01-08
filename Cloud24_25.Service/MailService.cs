using Cloud24_25.Infrastructure.Model;
using Resend;

namespace Cloud24_25.Service;

public static class MailService
{
    public static async Task SendConfirmationEmail(IResend resend, string to, string code, User user)
    {
        var message = new EmailMessage
        {
            From = new EmailAddress
            {
                Email = "cloud24-25@bdymek.com",
                DisplayName = "Cloud24-25 File Storage"
            },
            To = to,
            Subject = "Confirm your email",
            HtmlBody = 
                "<div>" +
                    $"<strong>Hi {user.UserName}!</strong>" +
                    "<strong>Your code is:</strong>" +
                    "<table style=\"border: 1px solid black;\">" +
                        "<tr>" +
                            $"<td>{code}</td>" +
                        "</tr>" +
                    "</table>" +
                "</div>"
        };
        
        await resend.EmailSendAsync( message );
    }
}