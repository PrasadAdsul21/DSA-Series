using System;
using System.Collections.Generic;
using System.Linq;

namespace FlightReservationApp
{
    // ==========================================
    // 1. ENUMS & DOMAIN MODELS
    // ==========================================

    public enum FlightStatus
    {
        Scheduled,
        Delayed,
        Departed,
        Cancelled
    }

    public enum BookingStatus
    {
        Confirmed,
        Cancelled
    }

    public class Flight
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public int TotalCapacity { get; set; }
        public int BookedSeats { get; set; }
        public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

        public int AvailableSeats => TotalCapacity - BookedSeats;
    }

    public class Booking
    {
        public Guid BookingId { get; set; } = Guid.NewGuid();
        public string FlightNumber { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string PassportOrId { get; set; } = string.Empty;
        public int SeatsReserved { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
        public DateTime BookingTimestamp { get; set; } = DateTime.UtcNow;
    }

    // ==========================================
    // 2. SERVICE LAYER (CRUD & EDGE-CASE ENGINE)
    // ==========================================

    public class FlightService
    {
        private readonly List<Flight> _flights = new();
        private readonly List<Booking> _bookings = new();
        private readonly object _lock = new();

        // --- FLIGHT CRUD ---

        public (bool Success, string Message) AddFlight(string flightNumber, string origin, string destination, DateTime departure, int capacity)
        {
            // Edge Case: Validate basic string inputs
            if (string.IsNullOrWhiteSpace(flightNumber) || string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
                return (false, "Error: Flight number, origin, and destination cannot be blank.");

            // Edge Case: Same origin and destination
            if (origin.Trim().Equals(destination.Trim(), StringComparison.OrdinalIgnoreCase))
                return (false, "Conflict: Origin and destination cannot be identical.");

            // Edge Case: Invalid capacity
            if (capacity <= 0)
                return (false, "Error: Aircraft capacity must be greater than zero.");

            // Edge Case: Past departure date
            if (departure <= DateTime.Now)
                return (false, "Error: Departure time must be in the future.");

            lock (_lock)
            {
                // Edge Case: Duplicate flight number
                if (_flights.Any(f => f.FlightNumber.Equals(flightNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return (false, $"Conflict: Flight '{flightNumber}' already exists in the system.");

                _flights.Add(new Flight
                {
                    FlightNumber = flightNumber.Trim().ToUpperInvariant(),
                    Origin = origin.Trim(),
                    Destination = destination.Trim(),
                    DepartureTime = departure,
                    TotalCapacity = capacity,
                    BookedSeats = 0,
                    Status = FlightStatus.Scheduled
                });
            }

            return (true, "Flight registered successfully.");
        }

        public IEnumerable<Flight> GetAllFlights() => _flights.ToList();

        public Flight? GetFlight(string flightNumber) =>
            _flights.FirstOrDefault(f => f.FlightNumber.Equals(flightNumber.Trim(), StringComparison.OrdinalIgnoreCase));

        public (bool Success, string Message) UpdateFlightStatus(string flightNumber, FlightStatus newStatus)
        {
            lock (_lock)
            {
                var flight = GetFlight(flightNumber);
                if (flight == null)
                    return (false, "Error: Flight not found.");

                // Edge Case: Disallow altering already departed or cancelled flights
                if (flight.Status == FlightStatus.Departed)
                    return (false, "Conflict: Cannot change status of an already departed flight.");

                if (flight.Status == FlightStatus.Cancelled && newStatus != FlightStatus.Scheduled)
                    return (false, "Conflict: Flight is already cancelled.");

                flight.Status = newStatus;
                return (true, $"Flight status updated to {newStatus}.");
            }
        }

        public (bool Success, string Message) CancelFlight(string flightNumber)
        {
            lock (_lock)
            {
                var flight = GetFlight(flightNumber);
                if (flight == null)
                    return (false, "Error: Flight not found.");

                if (flight.Status == FlightStatus.Departed)
                    return (false, "Conflict: Cannot cancel a flight that has already departed.");

                if (flight.Status == FlightStatus.Cancelled)
                    return (false, "Conflict: Flight is already cancelled.");

                flight.Status = FlightStatus.Cancelled;

                // Cascade: Mark all active bookings for this flight as cancelled
                var activeBookings = _bookings.Where(b => b.FlightNumber == flight.FlightNumber && b.Status == BookingStatus.Confirmed);
                foreach (var b in activeBookings)
                {
                    b.Status = BookingStatus.Cancelled;
                }

                flight.BookedSeats = 0;
                return (true, "Flight and all related bookings cancelled successfully.");
            }
        }

        // --- BOOKING CRUD ---

        public (bool Success, string Message, Guid? BookingId) CreateBooking(string flightNumber, string passengerName, string passengerId, int seatsToReserve)
        {
            if (string.IsNullOrWhiteSpace(passengerName) || string.IsNullOrWhiteSpace(passengerId))
                return (false, "Error: Passenger name and ID/Passport are required.", null);

            if (seatsToReserve <= 0)
                return (false, "Error: Reserved seats must be at least 1.", null);

            lock (_lock)
            {
                var flight = GetFlight(flightNumber);

                // Edge Case: Flight existence
                if (flight == null)
                    return (false, "Error: Flight does not exist.", null);

                // Edge Case: Flight availability based on operational status
                if (flight.Status == FlightStatus.Cancelled)
                    return (false, "Conflict: Cannot book on a cancelled flight.", null);

                if (flight.Status == FlightStatus.Departed)
                    return (false, "Conflict: Flight has already departed.", null);

                // Edge Case: Seat capacity and overbooking prevention
                if (flight.AvailableSeats < seatsToReserve)
                    return (false, $"Conflict: Insufficient seats. Requested: {seatsToReserve}, Available: {flight.AvailableSeats}.", null);

                // Edge Case: Double-booking protection (same passenger ID on same flight with active booking)
                bool alreadyBooked = _bookings.Any(b =>
                    b.FlightNumber == flight.FlightNumber &&
                    b.PassportOrId.Equals(passengerId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    b.Status == BookingStatus.Confirmed);

                if (alreadyBooked)
                    return (false, $"Conflict: Passenger with ID '{passengerId}' already has an active booking on this flight.", null);

                // Process booking
                flight.BookedSeats += seatsToReserve;

                var booking = new Booking
                {
                    FlightNumber = flight.FlightNumber,
                    PassengerName = passengerName.Trim(),
                    PassportOrId = passengerId.Trim().ToUpperInvariant(),
                    SeatsReserved = seatsToReserve,
                    Status = BookingStatus.Confirmed
                };

                _bookings.Add(booking);
                return (true, "Booking confirmed.", booking.BookingId);
            }
        }

        public (bool Success, string Message) CancelBooking(Guid bookingId)
        {
            lock (_lock)
            {
                var booking = _bookings.FirstOrDefault(b => b.BookingId == bookingId);
                if (booking == null)
                    return (false, "Error: Booking record not found.");

                if (booking.Status == BookingStatus.Cancelled)
                    return (false, "Conflict: Booking is already cancelled.");

                var flight = GetFlight(booking.FlightNumber);
                if (flight != null)
                {
                    // Edge Case: Flight already departed
                    if (flight.Status == FlightStatus.Departed)
                        return (false, "Conflict: Cannot cancel booking for an already departed flight.");

                    // Release seats
                    flight.BookedSeats = Math.Max(0, flight.BookedSeats - booking.SeatsReserved);
                }

                booking.Status = BookingStatus.Cancelled;
                return (true, "Booking cancelled and seats restored to pool.");
            }
        }

        public IEnumerable<Booking> GetBookingsForFlight(string flightNumber) =>
            _bookings.Where(b => b.FlightNumber.Equals(flightNumber.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // ==========================================
    // 3. UI CONSOLE INTERACTION
    // ==========================================

    internal class Program
    {
        private static readonly FlightService Service = new();

        private static void Main()
        {
            SeedInitialData();

            while (true)
            {
                Console.WriteLine("\n================ FLIGHT RESERVATION SYSTEM ================");
                Console.WriteLine("1. View All Flights");
                Console.WriteLine("2. Add New Flight");
                Console.WriteLine("3. Update Flight Status");
                Console.WriteLine("4. Cancel Flight");
                Console.WriteLine("5. Book a Ticket");
                Console.WriteLine("6. Cancel a Booking");
                Console.WriteLine("7. View Bookings by Flight");
                Console.WriteLine("8. Exit");
                Console.Write("Enter your choice (1-8): ");

                var input = Console.ReadLine()?.Trim();
                Console.WriteLine();

                switch (input)
                {
                    case "1": DisplayFlights(); break;
                    case "2": AddFlightUI(); break;
                    case "3": UpdateFlightStatusUI(); break;
                    case "4": CancelFlightUI(); break;
                    case "5": BookTicketUI(); break;
                    case "6": CancelBookingUI(); break;
                    case "7": ViewBookingsUI(); break;
                    case "8": return;
                    default: Console.WriteLine("Invalid option. Please choose between 1 and 8."); break;
                }
            }
        }

        private static void SeedInitialData()
        {
            Service.AddFlight("AI-101", "BOM", "DEL", DateTime.Now.AddDays(2), 180);
            Service.AddFlight("6E-205", "PNQ", "BLR", DateTime.Now.AddDays(1), 2);
        }

        private static void DisplayFlights()
        {
            var flights = Service.GetAllFlights().ToList();
            if (!flights.Any())
            {
                Console.WriteLine("No flights available.");
                return;
            }

            Console.WriteLine("{0,-10} {1,-8} {2,-8} {3,-20} {4,-10} {5,-10} {6,-10}",
                "FlightNo", "Origin", "Dest", "Departure", "Capacity", "Available", "Status");
            Console.WriteLine(new string('-', 80));

            foreach (var f in flights)
            {
                Console.WriteLine("{0,-10} {1,-8} {2,-8} {3,-20:yyyy-MM-dd HH:mm} {4,-10} {5,-10} {6,-10}",
                    f.FlightNumber, f.Origin, f.Destination, f.DepartureTime, f.TotalCapacity, f.AvailableSeats, f.Status);
            }
        }

        private static void AddFlightUI()
        {
            Console.Write("Flight Number: ");
            var num = Console.ReadLine() ?? "";

            Console.Write("Origin (e.g. BOM): ");
            var origin = Console.ReadLine() ?? "";

            Console.Write("Destination (e.g. DEL): ");
            var dest = Console.ReadLine() ?? "";

            Console.Write("Departure Date & Time (yyyy-MM-dd HH:mm): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var departure))
            {
                Console.WriteLine("Error: Invalid date format.");
                return;
            }

            Console.Write("Total Capacity: ");
            if (!int.TryParse(Console.ReadLine(), out var capacity))
            {
                Console.WriteLine("Error: Capacity must be an integer.");
                return;
            }

            var result = Service.AddFlight(num, origin, dest, departure, capacity);
            Console.WriteLine(result.Message);
        }

        private static void UpdateFlightStatusUI()
        {
            Console.Write("Enter Flight Number: ");
            var num = Console.ReadLine() ?? "";

            Console.WriteLine("Select Status: 0=Scheduled, 1=Delayed, 2=Departed, 3=Cancelled");
            if (!Enum.TryParse<FlightStatus>(Console.ReadLine(), out var status))
            {
                Console.WriteLine("Error: Invalid status value.");
                return;
            }

            var result = Service.UpdateFlightStatus(num, status);
            Console.WriteLine(result.Message);
        }

        private static void CancelFlightUI()
        {
            Console.Write("Enter Flight Number to Cancel: ");
            var num = Console.ReadLine() ?? "";

            var result = Service.CancelFlight(num);
            Console.WriteLine(result.Message);
        }

        private static void BookTicketUI()
        {
            Console.Write("Enter Flight Number: ");
            var num = Console.ReadLine() ?? "";

            Console.Write("Passenger Full Name: ");
            var name = Console.ReadLine() ?? "";

            Console.Write("Passenger Passport/ID Number: ");
            var id = Console.ReadLine() ?? "";

            Console.Write("Seats to Reserve: ");
            if (!int.TryParse(Console.ReadLine(), out var seats))
            {
                Console.WriteLine("Error: Seat count must be an integer.");
                return;
            }

            var result = Service.CreateBooking(num, name, id, seats);
            Console.WriteLine(result.Message);
            if (result.Success)
            {
                Console.WriteLine($"Generated Booking ID: {result.BookingId}");
            }
        }

        private static void CancelBookingUI()
        {
            Console.Write("Enter Booking ID (Guid): ");
            if (!Guid.TryParse(Console.ReadLine(), out var id))
            {
                Console.WriteLine("Error: Invalid GUID format.");
                return;
            }

            var result = Service.CancelBooking(id);
            Console.WriteLine(result.Message);
        }

        private static void ViewBookingsUI()
        {
            Console.Write("Enter Flight Number: ");
            var num = Console.ReadLine() ?? "";

            var bookings = Service.GetBookingsForFlight(num).ToList();
            if (!bookings.Any())
            {
                Console.WriteLine("No bookings found for this flight.");
                return;
            }

            Console.WriteLine("{0,-38} {1,-20} {2,-12} {3,-8} {4,-10}",
                "BookingID", "Passenger", "ID", "Seats", "Status");
            Console.WriteLine(new string('-', 92));

            foreach (var b in bookings)
            {
                Console.WriteLine("{0,-38} {1,-20} {2,-12} {3,-8} {4,-10}",
                    b.BookingId, b.PassengerName, b.PassportOrId, b.SeatsReserved, b.Status);
            }
        }
    }
}