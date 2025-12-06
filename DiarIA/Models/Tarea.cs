using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiarIA.Models
{
    public class Tarea
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Display(Name = "Título de la Tarea")]
        public string Nombre { get; set; }

        public string? Descripcion { get; set; }

        // --- FECHAS ---

        [Required(ErrorMessage = "Debes definir una fecha tope")]
        [Display(Name = "Fecha Límite (Deadline)")]
        [DataType(DataType.Date)]
        public DateTime FechaTope { get; set; }

        [Display(Name = "Fecha Agendada")]
        [DataType(DataType.Date)]
        public DateTime? FechaAgendada { get; set; } // Puede ser nula si la IA aún no la asigna

        // --- METADATA PARA LA IA ---

        [Required]
        [Display(Name = "Duración (minutos)")]
        public int DuracionMinutos { get; set; } = 30; // Valor por defecto

        [Required]
        public NivelPrioridad Prioridad { get; set; } = NivelPrioridad.Media;

        [Required]
        public NivelDificultad Dificultad { get; set; } = NivelDificultad.Medio;

        public bool Completada { get; set; }

        // --- RELACIÓN CON USUARIO ---
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual IdentityUser? Usuario { get; set; }
    }

    public enum NivelPrioridad
    {
        Baja = 1,
        Media = 2,
        Alta = 3,
        Urgente = 4
    }

    public enum NivelDificultad
    {
        Facil = 1,
        Medio = 2,
        Dificil = 3,
        Experto = 4
    }
}
