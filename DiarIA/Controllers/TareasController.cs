using DiarIA.Data;
using DiarIA.Models;
using DiarIA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager; 

        // Actualizamos el constructor agregando userManager
        public TareasController(DiarIAContext context,
                                IAIService aiService,
                                UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _aiService = aiService;
            _userManager = userManager; 
        }

        // --- ACCIÓN NUEVA: REORGANIZAR ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorganizar(string instruccionIA)
        {
            try
            {
                // --- INICIO BLOQUE DE SEGURIDAD PREMIUM ---
                var user = await _userManager.GetUserAsync(User); // Obtenemos el usuario completo

                if (user == null || !user.IsPremium) // Verificamos la bandera
                {
                    TempData["Error"] = "Esta función es exclusiva para usuarios Premium 💎";
                    return RedirectToAction(nameof(Index));
                }
                // --- FIN BLOQUE DE SEGURIDAD PREMIUM ---

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Traemos TODAS las tareas (incluso completadas si queremos permitir que la IA las reactive, 
                // pero por ahora filtramos para proteger datos históricos, aunque el usuario podría pedir 'marcar X como completada')
                // CAMBIO: Traemos todo lo que pertenezca al usuario para permitir ediciones globales.
                var misTareas = await _context.Tareas.Where(t => t.UserId == userId).ToListAsync();

                if (!misTareas.Any())
                {
                    TempData["Error"] = "No tienes tareas para procesar.";
                    return RedirectToAction(nameof(Index));
                }

                // Llamada a la IA
                var tareasSugeridas = await _aiService.ReorganizarTareasAsync(misTareas, instruccionIA);

                if (tareasSugeridas == null || !tareasSugeridas.Any())
                {
                    TempData["Info"] = "La IA no sugirió cambios.";
                    return RedirectToAction(nameof(Index));
                }

                int cambios = 0;
                int tareasVencidas = 0;

                foreach (var sugerencia in tareasSugeridas)
                {
                    var tareaOriginal = misTareas.FirstOrDefault(t => t.Id == sugerencia.Id);

                    // Verificamos que la tarea exista y pertenezca al usuario (seguridad extra)
                    if (tareaOriginal != null && tareaOriginal.UserId == userId)
                    {
                        bool tareaModificada = false;

                        // 1. Actualizar FECHA (Lógica original)
                        if (tareaOriginal.FechaAgendada != sugerencia.FechaAgendada)
                        {
                            tareaOriginal.FechaAgendada = sugerencia.FechaAgendada;
                            tareaModificada = true;
                        }

                        // 2. Actualizar DURACIÓN (Corrección QA #3)
                        if (tareaOriginal.DuracionMinutos != sugerencia.DuracionMinutos)
                        {
                            tareaOriginal.DuracionMinutos = sugerencia.DuracionMinutos;
                            tareaModificada = true;
                        }

                        // 3. Actualizar COMPLETADA (Corrección QA #7)
                        if (tareaOriginal.Completada != sugerencia.Completada)
                        {
                            tareaOriginal.Completada = sugerencia.Completada;
                            tareaModificada = true;
                        }

                        // 4. Actualizar PRIORIDAD
                        if (tareaOriginal.Prioridad != sugerencia.Prioridad)
                        {
                            tareaOriginal.Prioridad = sugerencia.Prioridad;
                            tareaModificada = true;
                        }

                        // Chequeo de Vencimiento
                        if (sugerencia.FechaAgendada.HasValue && sugerencia.FechaAgendada.Value > tareaOriginal.FechaTope)
                        {
                            tareasVencidas++;
                        }

                        if (tareaModificada)
                        {
                            _context.Update(tareaOriginal);
                            cambios++;
                        }
                    }
                }

                if (cambios > 0)
                {
                    await _context.SaveChangesAsync();
                    string mensaje = $"✅ IA Aplicada: {cambios} atributos actualizados.";
                    if (tareasVencidas > 0) mensaje += $" (Atención: {tareasVencidas} tareas exceden su fecha límite).";

                    TempData["Success"] = mensaje;
                }
                else
                {
                    TempData["Info"] = "La instrucción se entendió, pero los datos ya coinciden con lo solicitado.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Esto mostrará el mensaje amigable de "Content Filter" si ocurre
                TempData["Error"] = ex.Message;
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
