using DiarIA.Models; // Asegúrate de tener este using

namespace DiarIA.Services
{
    public interface IAIService
    {
        Task<string> ObtenerSugerenciaAsync(string promptUsuario, bool modoPrueba);

        // NUEVO MÉTODO:
        Task<List<Tarea>> ReorganizarTareasAsync(List<Tarea> tareasActuales, string instruccionUsuario);
    }
}