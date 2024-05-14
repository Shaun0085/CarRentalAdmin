using CarRentalAdmin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CarRentalAdmin.Controllers
{
    public class HomeController : Controller
    {
        private readonly CarRentalDatabaseContext _context;
        private readonly ILogger<HomeController> _logger;
        public class IndexViewModel
        {
            public List<Booking> Bookings { get; set; }
            public List<Car> CarList { get; set; }
        }

        public HomeController(ILogger<HomeController> logger, CarRentalDatabaseContext context)
        {
            _logger = logger;
            _context = context;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Login()
        {
            return View();
        }

        // Login Page Controller
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.UserName == username && a.Password == password);
            if (admin != null)
            {
                HttpContext.Session.SetString("UserName", admin.UserName);
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, admin.UserName),
        };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                var properties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
                };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return View();
            }
        }

        // Logout Controller
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Home");
        }

        // Index Page Controller
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index()
        {
            var userName = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToAction("Login", "Home");
            }
            var bookings = _context.Bookings
                    .Include(b => b.BookingStatus)
                    .Include(b => b.Vehicle)
                    .Include(b => b.Payment)
                    .Include(b => b.Location)
                    .Include(b => b.Car)
                    .Include(b => b.Billing)
                    .ToList();
            var carList = _context.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.Vehicle)
                .Where(c => c.CarStatus.Name == "Available")
                .Select(c => new Car
                {
                    CarId = c.CarId,
                    Name = c.Name,
                    PlateNumber = c.PlateNumber,
                    Year = c.Year,
                    Color = c.Color,
                    Notes = c.Notes,
                    Vehicle = c.Vehicle
                })
                .ToList();
            var numberOfCars = _context.Cars.Count();
            var numberOfVehicles = _context.Vehicles.Count();
            var totalNumberOfBookings = _context.Bookings.Count();
            ViewBag.NumberOfCars = numberOfCars;
            ViewBag.NumberOfVehicles = numberOfVehicles;
            ViewBag.TotalNumberOfBookings = totalNumberOfBookings;
            var viewModel = new IndexViewModel
            {
                Bookings = bookings,
                CarList = carList
            };
            return View(viewModel);
        }

        [HttpPost]
        public IActionResult AssignBookingCar(int bookingId, int carId)
        {
            try
            {
                var booking = _context.Bookings
                    .Include(b => b.Car)
                    .FirstOrDefault(b => b.BookingId == bookingId);
                if (booking != null)
                {
                    if (carId != 0)
                    {
                        var car = _context.Cars.FirstOrDefault(c => c.CarId == carId);
                        if (car != null)
                        {
                            if (booking.Car != null)
                            {
                                booking.Car.CarStatusId = 1;
                            }
                            car.CarStatusId = 3;
                            booking.Car = car;
                        }
                        else
                        {
                            return RedirectToAction("Error", new { message = "Car not found" });
                        }
                    }
                    else
                    {
                        // If "No Car Assigned" is selected, set booking's car to null
                        booking.Car = null;
                    }

                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }
                else
                {
                    return RedirectToAction("Error", new { message = "Booking not found" });
                }
            }
            catch (Exception ex)
            {
                return RedirectToAction("Error");
            }
        }

        // Confirm Booking Controller
        [HttpPost]
        public IActionResult ConfirmBooking(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Car).FirstOrDefault(b => b.BookingId == bookingId);
            if (booking != null)
            {
                if (booking.Car == null)
                {
                    return RedirectToAction("Index", new { showCarAssignmentModal = true });
                }
                booking.BookingStatus = _context.BookingStatuses.FirstOrDefault(s => s.Name.ToLower() == "ongoing");
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // Reject Booking Controller
        [HttpPost]
        public IActionResult RejectBooking(int bookingId)
        {
            var booking = _context.Bookings.Include(b => b.Car).FirstOrDefault(b => b.BookingId == bookingId);

            if (booking != null)
            {
                // Update booking status to 'Rejected'
                booking.BookingStatus = _context.BookingStatuses.FirstOrDefault(s => s.Name.ToLower() == "rejected");

                if (booking.Car != null)
                {
                    booking.Car.CarStatusId = 1;
                    booking.CarId = null; 
                }
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        // Complete Booking Controller
        [HttpPost]
        public IActionResult CompleteBooking(int bookingId)
        {
            var booking = _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefault(b => b.BookingId == bookingId);

            if (booking != null)
            {
                booking.BookingStatus = _context.BookingStatuses.FirstOrDefault(s => s.Name.ToLower() == "completed");

                if (booking.Car != null)
                {
                    booking.Car.CarStatusId = 1;
                    booking.CarId = null;
                }
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // Undo Booking Controller
        [HttpPost]
        public IActionResult UndoBooking(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking != null)
            {
                booking.BookingStatus = _context.BookingStatuses.FirstOrDefault(s => s.Name.ToLower() == "pending");
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // Delete Booking Controller
        [HttpPost]
        public IActionResult DeleteBooking(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking != null)
            {
                _context.Bookings.Remove(booking);
                _context.SaveChanges();
            }
            return Json(new { success = true });
        }

        // Restore Booking Controller
        [HttpPost]
        public IActionResult RestoreBooking(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking != null)
            {
                booking.BookingStatus = _context.BookingStatuses.FirstOrDefault(s => s.Name.ToLower() == "pending");
                _context.SaveChanges();
            }
            return RedirectToAction("Index"); 
        }

        // CarList Page Controller
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> CarList()
        {
            var userName = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToAction("Login", "Home");
            }

            var cars = await _context.Cars
                .Include(c => c.CarStatus)
                .Include(c => c.Vehicle)
                .Include(c => c.Bookings)
                .ToListAsync();

            // Retrieve all car statuses
            var allCarStatuses = await _context.CarStatuses.ToListAsync();

            // Pass all car statuses to the view
            ViewData["AllCarStatuses"] = allCarStatuses;

            // Filter out the "Occupied" status for add new car modal
            var carStatusesForAddNewCar = allCarStatuses
                .Where(cs => cs.Name != "Occupied")
                .ToList();

            ViewData["CarStatusesForAddNewCar"] = carStatusesForAddNewCar;

            var vehicles = await _context.Vehicles.ToListAsync();
            ViewData["Vehicles"] = vehicles;

            return View(cars);
        }

        //Add New Car Controller
        [HttpPost]
        public async Task<IActionResult> AddCar(Car model, int carStatus, int vehicleType)
        {
            if (ModelState.IsValid)
            {
                var car = new Car
                {
                    Name = model.Name,
                    PlateNumber = model.PlateNumber,
                    Year = model.Year,
                    Color = model.Color,
                    Notes = model.Notes,
                    CarStatusId = carStatus,
                    VehicleId = vehicleType 
                };

                _context.Cars.Add(car);
                await _context.SaveChangesAsync();
                return RedirectToAction("CarList");
            }
            return View("CarList", model);
        }

        //Fetch Car Details Controller
        [HttpGet]
        public async Task<IActionResult> GetCarDetails(int carId)
        {
            var car = await _context.Cars
                                   .Include(c => c.CarStatus)
                                   .Include(c => c.Vehicle)
                                   .FirstOrDefaultAsync(c => c.CarId == carId);
            if (car == null)
            {
                return NotFound();
            }
            return Json(new
            {
                name = car.Name,
                plateNumber = car.PlateNumber,
                year = car.Year,
                color = car.Color,
                notes = car.Notes,
                status = car.CarStatus.CarStatusId,
                vehicle = car.Vehicle.VehicleId 
            });
        }

        //Edit Car Controller
        [HttpPost]
        public async Task<IActionResult> EditCar(Car model)
        {
            if (ModelState.IsValid)
            {
                var existingCar = await _context.Cars.FindAsync(model.CarId);
                if (existingCar != null)
                {
                    var vehicle = await _context.Vehicles.FindAsync(model.VehicleId);
                    var carStatus = await _context.CarStatuses.FindAsync(model.CarStatusId);

                    existingCar.Vehicle = vehicle;
                    existingCar.Name = model.Name;
                    existingCar.PlateNumber = model.PlateNumber;
                    existingCar.Year = model.Year;
                    existingCar.Color = model.Color;
                    existingCar.Notes = model.Notes;
                    existingCar.CarStatus = carStatus;
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction("CarList");
            }
            else
            {
                return View("CarList", model);
            }
        }

        // Delete Car Controller
        [HttpPost]
        public async Task<IActionResult> DeleteCar(int carId)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null)
            {
                return Json(new { success = false, errorMessage = "Car not found" });
            }
            _context.Cars.Remove(car);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}