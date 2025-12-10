using DiarIA.Models;
using DiarIA.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace DiarIA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAIService _aiService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, IAIService aiService, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _aiService = aiService;
            _userManager = userManager;
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
        [Authorize]
        public async Task<IActionResult> TestAI()
        {
            var user = await _userManager.GetUserAsync(User);

            // Pasamos el estado a la vista usando ViewBag
            if (user != null)
            {
                ViewBag.IsPremium = user.IsPremium;
            }
            else
            {
                ViewBag.IsPremium = false; // Si no está logueado, no es premium
            }

            return View();
        }

        // POST: Recibe los datos y llama al servicio
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> TestAI(string prompt, bool modoPrueba)
        {
            var user = await _userManager.GetUserAsync(User);

            // Verificación de seguridad
            if (user == null || !user.IsPremium)
            {
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                ViewBag.Respuesta = "Escribe algo primero.";
                ViewBag.IsPremium = true; // <--- IMPORTANTE: Mantener la vista premium
                return View();
            }

            var resultado = await _aiService.ObtenerSugerenciaAsync(prompt, modoPrueba);

            ViewBag.Respuesta = resultado;
            ViewBag.LastPrompt = prompt;

            // ?? ESTA ES LA LÍNEA QUE TE FALTABA ??
            ViewBag.IsPremium = true;

            return View();
        }
        #endregion
    }
}
