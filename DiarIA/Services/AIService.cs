using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using System.Text.Json; // Necesario para JSON
using System.Diagnostics; // Necesario para Debug.WriteLine
using DiarIA.Models; // Necesario para Tarea

namespace DiarIA.Services
{
    public class AIService : IAIService
    {
        private readonly IConfiguration _configuration;

        public AIService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // --- MÉTODO 1: SUGERENCIA (TEXTO) ---
        public async Task<string> ObtenerSugerenciaAsync(string promptUsuario, bool modoPrueba)
        {
            if (modoPrueba)
            {
                await Task.Delay(500);
                return "🤖 [MODO PRUEBA] Respuesta simulada...";
            }

            try
            {
                var chatClient = ObtenerClienteChat();
                List<ChatMessage> messages = new List<ChatMessage>()
                {
                    new SystemChatMessage("Eres un asistente personal útil."),
                    new UserChatMessage(promptUsuario)
                };

                ChatCompletion completion = await chatClient.CompleteChatAsync(messages);
                return completion.Content[0].Text;
            }
            catch (Exception ex)
            {
                return $"❌ Error: {ex.Message}";
            }
        }

        // --- MÉTODO 2: REORGANIZAR TAREAS (CON DEBUG) ---
        public async Task<List<Tarea>> ReorganizarTareasAsync(List<Tarea> tareasActuales, string instruccionUsuario)
        {
            try
            {
                // 1. Serializar
                var tareasLight = tareasActuales.Select(t => new
                {
                    t.Id,
                    t.Nombre,
                    t.DuracionMinutos,
                    t.Prioridad,
                    FechaAgendada = t.FechaAgendada?.ToString("yyyy-MM-ddTHH:mm:ss"), // Agrega la T y los segundos
                    t.FechaTope
                });
                string jsonEntrada = JsonSerializer.Serialize(tareasLight);
                string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                // LOG: Ver qué enviamos
                Debug.WriteLine($"[IA REQUEST] Enviando {tareasActuales.Count} tareas. Instrucción: {instruccionUsuario}");

                // 2. Prompt
                string systemPrompt = $@"
                    Eres un experto en gestión del tiempo. Hoy es: {fechaHoy}.
                    TU OBJETIVO: Reorganizar las fechas ('FechaAgendada') según la instrucción.
                    REGLAS CRÍTICAS:
                    1. Devuelve SOLO un JSON válido con las tareas modificadas.
                    2. NO incluyas texto fuera del JSON (sin ```json ni ```).
                    3. NO cambies los ID.
                    4. Formato fecha OBLIGATORIO: ISO 8601 estricto 'yyyy-MM-ddTHH:mm:ss' (ej: 2023-12-31T15:00:00).
                ";

                var chatClient = ObtenerClienteChat();
                List<ChatMessage> messages = new List<ChatMessage>()
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage($"TAREAS: {jsonEntrada}\n\nINSTRUCCIÓN: {instruccionUsuario}")
                };

                // 3. Llamada
                ChatCompletionOptions options = new ChatCompletionOptions()
                {
                    Temperature = 0.1f, // Muy preciso
                    MaxOutputTokenCount = 4000
                };

                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);
                string respuestaTexto = completion.Content[0].Text;

                // LOG: Ver qué respondió la IA (CRUCIAL)
                Debug.WriteLine("--------------------------------------------------");
                Debug.WriteLine($"[IA RESPONSE RAW]: \n{respuestaTexto}");
                Debug.WriteLine("--------------------------------------------------");

                // 4. Limpieza
                respuestaTexto = respuestaTexto.Replace("```json", "").Replace("```", "").Trim();

                // 5. Deserializar
                var opcionesJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var tareasSugeridas = JsonSerializer.Deserialize<List<Tarea>>(respuestaTexto, opcionesJson);

                return tareasSugeridas ?? new List<Tarea>();
            }
            catch (Exception ex)
            {
                // LOG: Ver el error real
                Debug.WriteLine($"[IA ERROR]: {ex.Message}");

                // ¡IMPORTANTE! Lanzamos la excepción de nuevo para que el Controlador la vea
                // y te la muestre en la pantalla en lugar de silenciarla.
                throw new Exception($"Fallo interno IA: {ex.Message}", ex);
            }
        }

        private ChatClient ObtenerClienteChat()
        {
            string endpoint = _configuration["AzureOpenAI:Endpoint"];
            string key = _configuration["AzureOpenAI:ApiKey"];
            string deploymentName = _configuration["AzureOpenAI:DeploymentName"];

            AzureOpenAIClient azureClient = new AzureOpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(key)
            );
            return azureClient.GetChatClient(deploymentName);
        }
    }
}