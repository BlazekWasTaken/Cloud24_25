using Cloud24_25.Infrastructure.Model;
using Resend;

namespace Cloud24_25.Service;

public static class MailService
{
    public static async Task<ResendResponse> SendConfirmationEmail(IResend resend, User user)
    {
        var message = new EmailMessage
        {
            From = new EmailAddress
            {
                Email = "cloud24-25@bdymek.com",
                DisplayName = "Cloud24-25 File Storage"
            },
            To = user.Email,
            Subject = "Confirm your email",
            HtmlBody = 
                "<div style=\"text-align: center;\">" +
                    $"<p><strong>Hi {user.UserName}!</strong></p>" +
                    "<p>Your code is:</p>" +
                    "<table style=\"border: 1px solid black; border-spacing: 10px; margin: 0 auto;\">" +
                        "<tr>" +
                            $"<td>{user.ConfirmationCode}</td>" +
                        "</tr>" +
                    "</table>" +
                "</div>"
        };
        return await resend.EmailSendAsync( message );
    }
}