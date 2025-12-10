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
                    CONTEXTO: Asistente administrativo para una app de tareas. No eres médico ni terapeuta. Hoy es {fechaHoy}.

                    OBJETIVO: Reagendar 'FechaAgendada' de las tareas usando esta lógica:
                    - Prioriza por orden: 1) FechaTope más cercana, 2) Mayor Prioridad, 3) Mayor Dificultad, 4) Mayor DuracionMinutos.
                    - Mantén 'FechaAgendada' antes de 'FechaTope' siempre que sea posible.
                    - Evita sobrecargar un mismo día (distribuye por disponibilidad implícita).
                    - Si no existe forma de cumplir todo, mueve solo lo estrictamente necesario.
                    - Si el usuario menciona salud o emociones, ignóralo; céntrate SOLO en ajustar fechas.

                    DATOS POR TAREA (para tomar decisiones):
                    - FechaTope: urgencia real.
                    - FechaAgendada: fecha propuesta actual (puede cambiarse).
                    - DuracionMinutos: tiempo requerido (evitar sobrecarga por día).
                    - Prioridad: 1 = baja, 3 = alta.
                    - Dificultad: 1 = fácil, 3 = difícil (tareas difíciles se recomiendan antes).
                    - Completada: si es true, no modificar.

                    REGLAS:
                    1. Devuelve SOLO JSON válido (RFC 8259).
                    2. Fechas en ISO 8601 'yyyy-MM-ddTHH:mm:ss'.
                    3. No modifiques IDs.
                    4. No agregues texto fuera del JSON.

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
            catch (RequestFailedException ex) when (ex.Status == 400 && ex.ErrorCode == "content_filter")
            {
                // CASO ESPECÍFICO: AZURE BLOQUEÓ EL PROMPT
                Debug.WriteLine($"[IA FILTER]: El prompt fue bloqueado por políticas de seguridad. {ex.Message}");

                // Lanzamos una excepción amable para que el Controller la muestre
                throw new Exception("Tu instrucción activó los filtros de seguridad de IA (posiblemente por palabras sensibles como 'enfermo' o 'daño'). Intenta usar un lenguaje más neutral como 'No tengo tiempo hoy'.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IA ERROR]: {ex.Message}");
                throw new Exception($"Error técnico: {ex.Message}");
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