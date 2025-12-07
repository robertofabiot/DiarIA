using System.Diagnostics;
using DiarIA.Models;
using Microsoft.AspNetCore.Mvc;
using DiarIA.Services;

namespace DiarIA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAIService _aiService;

        public HomeController(ILogger<HomeController> logger, IAIService aiService)
        {
            _logger = logger;
            _aiService = aiService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #region AI
        // GET: Muestra el formulario vacío
        public IActionResult TestAI()
        {
            return View();
        }

        // POST: Recibe los datos y llama al servicio
        [HttpPost]
        public async Task<IActionResult> TestAI(string prompt, bool modoPrueba)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                ViewBag.Respuesta = "Escribe algo primero.";
                return View();
            }

            // Llamamos a tu nuevo servicio
            var resultado = await _aiService.ObtenerSugerenciaAsync(prompt, modoPrueba);

            ViewBag.Respuesta = resultado;
            ViewBag.LastPrompt = prompt; // Para no borrar lo que escribiste

            return View();
        }
        #endregion
    }
}
