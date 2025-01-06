using Resend;

namespace Cloud24_25.Service;

public static class MailService
{
    public static async Task SendConfirmationEmail(IResend resend, string to, string code, Guid userId)
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
            HtmlBody = "<div>" +
                       "<strong>Confirm your email using the link below</strong>" +
                       "</div>" +
                       "<div>" +
                       "<form action=\"https://localhost:7039/user/confirm-email\" method=\"post\" >" +
                       "<input type=\"hidden\" name=\"id\" value=\"" + userId + "\">" +
                       "<input type=\"hidden\" name=\"code\" value=\"" + code + "\">" +
                       "<input type=\"submit\" value=\"Confirm email\" >" +
                       "</form>" +
                       "</div>"
        };
        
        await resend.EmailSendAsync( message );
    }
}