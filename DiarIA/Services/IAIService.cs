using System.Threading.Tasks;

namespace DiarIA.Services
{
    public interface IAIService
    {
        // Este es el contrato que AIService debe cumplir
        Task<string> ObtenerSugerenciaAsync(string promptUsuario, bool modoPrueba);
    }
}