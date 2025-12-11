using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace DiarIA.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            string connectionString = _configuration["AzureEmail:ConnectionString"];
            string senderAddress = _configuration["AzureEmail:SenderAddress"];

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(senderAddress))
            {
                throw new InvalidOperationException("Faltan las credenciales de AzureEmail en appsettings.json");
            }

            var emailClient = new EmailClient(connectionString);

            // Envolver el mensaje de Identity en nuestra plantilla HTML
            string cuerpoFinal = ObtenerPlantillaHtml(subject, htmlMessage);

            try
            {
                var emailMessage = new EmailMessage(
                    senderAddress: senderAddress,
                    content: new EmailContent(subject) { Html = cuerpoFinal },
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(email) }));

                // WaitUntil.Started envía el correo y retorna rápido sin esperar confirmación total
                await emailClient.SendAsync(WaitUntil.Started, emailMessage);
            }
            catch (Exception ex)
            {
                // Aquí podrías loguear el error
                throw new Exception($"Error enviando correo Azure: {ex.Message}");
            }
        }

        private string ObtenerPlantillaHtml(string titulo, string contenido)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', sans-serif; background-color: #f8f9fa; padding: 20px; }}
                    .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); overflow: hidden; }}
                    .header {{ background-color: #0d6efd; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 30px; color: #333; line-height: 1.6; }}
                    .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; color: #666; }}
                    a {{ color: #0d6efd; font-weight: bold; text-decoration: none; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>🧠 DiarIA</h1>
                    </div>
                    <div class='content'>
                        <h2 style='margin-top:0;'>{titulo}</h2>
                        <p>{contenido}</p>
                    </div>
                    <div class='footer'>
                        <p>Enviado automáticamente por DiarIA via Azure Communication Services.</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}