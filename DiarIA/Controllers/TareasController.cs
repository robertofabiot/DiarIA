using DiarIA.Data;
using DiarIA.Models;
using DiarIA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DiarIA.Controllers
{
    [Authorize]
    public class TareasController : Controller
    {
        private readonly DiarIAContext _context;
        private readonly IAIService _aiService;

        public TareasController(DiarIAContext context, IAIService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        // --- ACCIÓN NUEVA: REORGANIZAR ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorganizar(string instruccionIA)
        {
            try
            {
                // 1. Obtener ID del usuario
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // 2. Obtener tareas pendientes de este usuario
                var misTareas = await _context.Tareas
                                              .Where(t => t.UserId == userId && !t.Completada)
                                              .ToListAsync();

                if (!misTareas.Any())
                {
                    TempData["Error"] = "No tienes tareas pendientes para reorganizar.";
                    return RedirectToAction(nameof(Index));
                }

                // 3. Llamar a la IA
                // (Asegúrate de haber actualizado IAIService con el método ReorganizarTareasAsync)
                var tareasSugeridas = await _aiService.ReorganizarTareasAsync(misTareas, instruccionIA);

                if (tareasSugeridas == null || !tareasSugeridas.Any())
                {
                    // Si llega aquí sin excepción, es que la IA devolvió una lista vacía válida (ningún cambio)
                    TempData["Info"] = "La IA analizó tu agenda pero no encontró cambios necesarios.";
                    return RedirectToAction(nameof(Index));
                }

                // 4. Guardar cambios en la Base de Datos
                int cambios = 0;
                foreach (var sugerencia in tareasSugeridas)
                {
                    var tareaOriginal = misTareas.FirstOrDefault(t => t.Id == sugerencia.Id);
                    if (tareaOriginal != null)
                    {
                        // Si la fecha cambió, la actualizamos
                        if (tareaOriginal.FechaAgendada != sugerencia.FechaAgendada)
                        {
                            tareaOriginal.FechaAgendada = sugerencia.FechaAgendada;
                            _context.Update(tareaOriginal);
                            cambios++;
                        }
                    }
                }

                if (cambios > 0)
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"¡Hecho! Se reprogramaron {cambios} tareas.";
                }
                else
                {
                    TempData["Info"] = "Tu agenda ya cumple con esa instrucción.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // AQUÍ ESTÁ EL DEBUG QUE NECESITAS
                // Te mostrará el error técnico exacto en la alerta roja de la página web
                TempData["Error"] = $"DEBUG ERROR: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Tareas
        public async Task<IActionResult> Index()
        {
            // Obtener el ID del usuario logueado
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Filtrar: Traer solo las tareas donde UserId == mi ID
            var misTareas = await _context.Tareas
                                          .Where(t => t.UserId == userId)
                                          .ToListAsync();

            return View(misTareas);
        }

        // GET: Tareas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tarea = await _context.Tareas
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tarea == null)
            {
                return NotFound();
            }

            return View(tarea);
        }

        // GET: Tareas/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Tareas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Descripcion,FechaTope,FechaAgendada,DuracionMinutos,Prioridad,Dificultad,Completada,UserId")] Tarea tarea)
        {
            if (ModelState.IsValid)
            {
                tarea.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Add(tarea);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tarea);
        }

        // GET: Tareas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea == null)
            {
                return NotFound();
            }
            return View(tarea);
        }

        // POST: Tareas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Descripcion,FechaTope,FechaAgendada,DuracionMinutos,Prioridad,Dificultad,Completada,UserId")] Tarea tarea)
        {
            if (id != tarea.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    tarea.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                    _context.Update(tarea);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TareaExists(tarea.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tarea);
        }

        // GET: Tareas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tarea = await _context.Tareas
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tarea == null)
            {
                return NotFound();
            }

            return View(tarea);
        }

        // POST: Tareas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea != null)
            {
                _context.Tareas.Remove(tarea);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TareaExists(int id)
        {
            return _context.Tareas.Any(e => e.Id == id);
        }
    }
}
