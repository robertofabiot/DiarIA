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
                // 1. Serializar con formato explícito para darle el ejemplo a la IA
                var tareasLight = tareasActuales.Select(t => new
                {
                    t.Id,
                    t.Nombre,
                    t.DuracionMinutos,
                    t.Prioridad,
                    t.Dificultad,
                    t.Completada,
                    // Forzamos el formato en la entrada para que la IA vea el patrón
                    FechaAgendada = t.FechaAgendada?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    FechaTope = t.FechaTope.ToString("yyyy-MM-ddTHH:mm:ss")
                });

                string jsonEntrada = JsonSerializer.Serialize(tareasLight);
                string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd dddd");

                // 2. Prompt BLINDADO
                string systemPrompt = $@"
            ERES: Un motor de planificación logística experto. Hoy es {fechaHoy}.
            ENTRADA: Un JSON con tareas y una instrucción del usuario.
            SALIDA: Un JSON válido con las tareas modificadas.

            --- REGLAS DE FORMATO TÉCNICO (CRÍTICAS) ---
            1. FECHAS: Debes usar ESTRICTAMENTE el formato ISO 8601 extendido: 'yyyy-MM-ddTHH:mm:ss'. 
               - CORRECTO: '2025-12-13T10:00:00'
               - INCORRECTO: '13/12/2025', '2025-12-13 10:00' (falta la T).
            2. JSON PURO: Tu respuesta debe ser SOLO el array JSON. No uses bloques de código markdown (```json).
            3. INTEGRIDAD: Devuelve siempre el objeto completo con su ID original.

            --- LÓGICA DE NEGOCIO ---
            1. INSTRUCCIONES DEL USUARIO: Tienen prioridad absoluta. 
               - Si pide cambiar duración, prioridad o estado ('completada'), MODIFICA esos campos.
               - Si dice 'Libera el día X', NO dejes la fecha nula (null). MUEVE esas tareas al siguiente día hábil disponible.
               - Si dice 'Estoy enfermo', mueve las tareas urgentes 2 días hacia adelante.
            2. AGENDAMIENTO AUTOMÁTICO:
               - Prioriza: 1) FechaTope cercana, 2) Urgencia, 3) Dificultad.
        ";

                var chatClient = ObtenerClienteChat();
                List<ChatMessage> messages = new List<ChatMessage>()
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage($"DATA: {jsonEntrada}\n\nCOMMAND: {instruccionUsuario}")
        };

                ChatCompletionOptions options = new ChatCompletionOptions()
                {
                    Temperature = 0.2f,
                    MaxOutputTokenCount = 4000
                };

                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

                // Validación básica
                if (completion.Content == null || completion.Content.Count == 0)
                {
                    throw new Exception("La IA no devolvió ninguna respuesta.");
                }

                string respuestaTexto = completion.Content[0].Text;

                // Limpieza de Markdown (por si acaso la IA ignora la regla 2 del formato)
                if (respuestaTexto.StartsWith("```json")) respuestaTexto = respuestaTexto.Substring(7);
                if (respuestaTexto.StartsWith("```")) respuestaTexto = respuestaTexto.Substring(3);
                if (respuestaTexto.EndsWith("```")) respuestaTexto = respuestaTexto.Substring(0, respuestaTexto.Length - 3);
                respuestaTexto = respuestaTexto.Trim();

                // 3. Deserializar
                // Usamos PropertyNameCaseInsensitive para evitar errores si la IA pone "id" minúscula
                var opcionesJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var tareasSugeridas = JsonSerializer.Deserialize<List<Tarea>>(respuestaTexto, opcionesJson);

                return tareasSugeridas ?? new List<Tarea>();
            }
            catch (RequestFailedException ex) when (ex.Status == 400 && ex.ErrorCode == "content_filter")
            {
                throw new Exception("Tu instrucción contiene palabras bloqueadas por seguridad (violencia, autolesión, etc). Por favor reformula.");
            }
            catch (JsonException ex)
            {
                // Este catch capturará el error si la IA falla en el formato de fecha
                Debug.WriteLine($"[JSON ERROR]: {ex.Message}");
                throw new Exception("Error interpretando la respuesta de la IA. Posiblemente un formato de fecha inválido. Inténtalo de nuevo.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IA ERROR]: {ex.Message}");
                throw;
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