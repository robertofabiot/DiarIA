using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat; // Importante: El nuevo SDK usa este namespace
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DiarIA.Services
{
    public class AIService : IAIService
    {
        private readonly IConfiguration _configuration;

        public AIService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> ObtenerSugerenciaAsync(string promptUsuario, bool modoPrueba)
        {
            // --- 1. MODO PRUEBA (Check box activado) ---
            // Esto es vital para no gastar créditos mientras Dev B prueba la interfaz
            if (modoPrueba)
            {
                await Task.Delay(500); // Simular latencia
                return "🤖 [MODO PRUEBA AZURE] \n\n" +
                       "Respuesta simulada: Veo que estás usando el modelo gpt-4o. " +
                       "Mi consejo es que descanses un poco y verifiques tu conexión. \n" +
                       "(Esta respuesta no consumió tokens reales).";
            }

            // --- 2. MODO REAL (Azure OpenAI SDK) ---
            try
            {
                // Leer configuración
                string endpoint = _configuration["AzureOpenAI:Endpoint"];
                string key = _configuration["AzureOpenAI:ApiKey"];
                string deploymentName = _configuration["AzureOpenAI:DeploymentName"]; // "gpt-4o"

                // Crear el cliente de Azure
                AzureOpenAIClient azureClient = new AzureOpenAIClient(
                    new Uri(endpoint),
                    new AzureKeyCredential(key)
                );

                // Obtener el cliente de chat específico para tu modelo
                ChatClient chatClient = azureClient.GetChatClient(deploymentName);

                // Preparar los mensajes (System y User)
                List<ChatMessage> messages = new List<ChatMessage>()
                {
                    new SystemChatMessage("Eres un asistente personal empático y conciso. Ayudas a organizar tareas y das consejos emocionales breves."),
                    new UserChatMessage(promptUsuario)
                };

                // Opciones adicionales (opcional, pero recomendado para controlar costos)
                ChatCompletionOptions options = new ChatCompletionOptions()
                {
                    MaxOutputTokenCount = 300, // Limitar longitud de respuesta
                    Temperature = 0.7f         // Creatividad balanceada
                };

                // Llamada asíncrona a Azure
                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

                // Retornar el texto
                return completion.Content[0].Text;
            }
            catch (Exception ex)
            {
                // Manejo de errores (ej: Clave incorrecta, cuota excedida)
                return $"❌ Error conectando con Azure OpenAI: {ex.Message}";
            }
        }
    }
}