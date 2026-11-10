===============================================================================
 ExpertLink - Consultation Appointment System
 BMIT2023 Web and Mobile Systems - Assignment
===============================================================================

Technology : .NET 10, ASP.NET Core MVC, C#, Entity Framework Core 10 (code-first,
             data annotations), SQL Server Express LocalDB (file-based .mdf),
             SignalR, jQuery, Bootstrap 5.3
Roles      : Admin, Consultant, Client


-------------------------------------------------------------------------------
 1. HOW TO RUN
-------------------------------------------------------------------------------
 1. Open "Consultation Appointment System (WEB AND MOBILE ASSIGNMENT).slnx"
    in Visual Studio 2026.
 2. Build the solution once (NuGet packages restore automatically).
 3. Create the database with a migration. Open
    Tools > NuGet Package Manager > Package Manager Console and run:

        Update-Database

    The migrations are already included in the Migrations folder. This
    creates ExpertLinkDb.mdf in the project folder. (The app also applies
    pending migrations automatically when it starts.)
 4. Press F5. On first start the app inserts the sample data
    (Data/DbSeeder.cs) automatically.

 To reset the database: stop the app, run "Drop-Database" in the Package
 Manager Console, then "Update-Database" and start the app again.


-------------------------------------------------------------------------------
 2. LOGIN CREDENTIALS
-------------------------------------------------------------------------------
 Role        Email                          Password
 ----------  -----------------------------  -----------
 Admin       admin@expertlink.com           Admin@123
 Admin       aisyah.admin@expertlink.com    Admin@123
 Consultant  consultant@expertlink.com      Consult@123   (Tan Wei Ming - main demo consultant)
 Consultant  priya@expertlink.com           Consult@123
 Consultant  jason@expertlink.com           Consult@123
 Consultant  farah@expertlink.com           Consult@123
 Consultant  ahmad@expertlink.com           Consult@123
 Consultant  meiling@expertlink.com         Consult@123
 Consultant  rajesh@expertlink.com          Consult@123
 Consultant  sitihajar@expertlink.com       Consult@123
 Consultant  kevin@expertlink.com           Consult@123   (pending admin approval)
 Client      client@expertlink.com          Client@123    (Lee Jia Hui - main demo client)
 Client      hafiz@example.com              Client@123
 Client      kavitha@example.com            Client@123
 Client      ckng@example.com               Client@123
 Client      aina@example.com               Client@123
 Client      daniel@example.com             Client@123
 Client      amirul@example.com             Client@123
 Client      unverified@example.com         Client@123    (email NOT verified - shows resend link)

 Test payment card : 4111 1111 1111 1111, any future expiry (e.g. 12/29), any CVV
                     (simulated gateway - "Card" option)
 Stripe test card  : 4242 4242 4242 4242, any future expiry, any CVC
                     ("Stripe" option - needs internet and a Stripe test key)


-------------------------------------------------------------------------------
 3. DEMO NOTES
-------------------------------------------------------------------------------
 * E-mail: fill in "Smtp" in appsettings.json (e.g. a Gmail address and an
   App Password) to send real e-mails. If SMTP is empty, every e-mail is saved
   as an .html file in the "MailDrop" folder, and verification / reset links
   are also shown on screen as a "Demo link".
 * Login blocking: enter a wrong password 3 times -> account locked 5 minutes
   (admin can unlock it in Users > View). A captcha appears after a failed attempt.
 * QR check-in: log in as client@expertlink.com, open TODAY's appointment with
   Tan Wei Ming, and show the e-ticket QR. In another browser log in as
   consultant@expertlink.com > Check-In > Start Camera (or "Scan from Image"
   using the saved QR PNG).
 * Real-time chat / notifications: open the same appointment as the client and
   the consultant in two different browsers (e.g. Chrome + Edge).
 * Stripe (test mode): create a free Stripe account, copy the TEST secret key
   (Developers > API keys, starts with sk_test_) into appsettings.Local.json:
       "Stripe": { "SecretKey": "sk_test_..." }
   The "Stripe" payment option then appears on the payment page. Payments and
   refunds (consultant reject / client cancel >= 24 h) show in the Stripe
   Dashboard (test mode). Without a key, the Stripe option is hidden.
 * Unpaid bookings are auto-cancelled after 30 minutes and the slot is released.
 * Cancellation >= 24 hours before the session = full refund; later = no refund.


-------------------------------------------------------------------------------
 4. SYSTEM MODULES OUTLINE  (replace Member A-D with your team members)
-------------------------------------------------------------------------------
 [*] = additional feature

 MEMBER A - Security & User Maintenance
   Security module
     - Login / logout (manual cookie authentication, role-based authorization)
     - Client registration, consultant application
     - Profile maintenance, change password
     - [*] Self-written SVG captcha (register, forgot password, after a failed login)
     - [*] Temporary login blocking after 3 failed attempts
     - [*] E-mail verification with token link (24 h expiry)
     - [*] Password reset with token link (30 min expiry)
     - [*] Webcam photo capture + photo editor (zoom, pan, rotate, flip, B&W, crop)
     - [*] User preference: light / dark theme (saved to DB + cookie)
   User Maintenance (Admin)
     - List users with AJAX search / filter / sort / paging
     - View user, activate / deactivate, unlock, create admin / client account
     - Approve / reject consultant applications

 MEMBER B - Category, Consultant Profile & Directory
   Category Maintenance (Admin)
     - Create / edit / delete / activate categories, remote name validation
     - [*] Batch delete and batch activate / deactivate (checkbox selection)
     - [*] Batch insert from CSV file with drag & drop and preview
   Consultant Profile & Service Maintenance (Consultant)
     - Edit professional profile (title, bio, qualification, specialty)
     - Service (consultation package) CRUD: duration, fee, online / in-person
     - [*] Multiple certificate photos per consultant, multiple upload per
           submission with drag & drop and preview
     - [*] Maps integration: pick office location (Leaflet + OpenStreetMap,
           address search, current location), map + directions on profile
   Consultant Directory (Public / Client)
     - Browse and view consultant profile, services, reviews, weekly hours
     - [*] AJAX search, filter (specialty, mode, max fee), sort and paging

 MEMBER C - Scheduling & Booking
   Availability Maintenance (Consultant)
     - Weekly schedule with multiple time ranges per day
       [*] batch insert / update / delete rows, quick-fill presets, overlap checks
     - Time off (block dates) with clash check against active appointments
   Booking (Client)
     - [*] Interactive on-screen calendar + time-slot selection (AJAX, shows
           free-slot count per day, morning / afternoon grouping)
     - Book a service (choose mode, describe concern), slot-clash prevention
       (consultant and client double booking), 2-hour lead time
     - Reschedule (>= 24 h before, max 2 times) and cancel with refund policy
     - [*] Auto-cancel unpaid bookings after 30 minutes (slot released)
   Appointment History (Client)
     - Tabs: upcoming / awaiting confirmation / past / cancelled, search, paging
     - [*] QR code e-ticket (generate) and "Add to Calendar" (.ics file)

 MEMBER D - Appointment Processing, Payment, Reviews & Reports
   Appointment Processing (Consultant)
     - Confirm (with meeting link for online sessions), reject with refund,
       mark completed with session notes, mark no-show
     - [*] QR code check-in by scanning the client's e-ticket with the webcam
   Payment (Client)
     - Checkout with Card / FPX / E-Wallet (simulated gateway), refunds
     - [*] 3rd-party payment API: Stripe Checkout (test mode), payment verified
           with the Stripe API, automatic Stripe refunds
     - [*] Card validation: Luhn checksum + expiry (custom attribute, client + server),
           live card preview and formatting
     - [*] E-receipt (printable / save as PDF + e-mailed HTML receipt)
   Reviews
     - Client rates completed sessions (1-5 stars), consultant replies,
       admin moderation (delete)
   Admin Dashboard & Reports
     - [*] On-screen charts with Chart.js (bookings & revenue trend, status,
           revenue by specialty, top consultants), period filter
     - [*] Export appointments report to CSV
   Communication
     - [*] Real-time chat per appointment (SignalR) with typing indicator
     - [*] Real-time notifications (SignalR push, toast, notification sound,
           navbar bell with unread count)


-------------------------------------------------------------------------------
 5. PROJECT STRUCTURE
-------------------------------------------------------------------------------
 Controllers/   Home, Account, Admin, Category, Consultants (public),
                Consultant (self-service), Booking, Appointment, Notification
 Data/          AppDbContext (code-first DbContext), DbSeeder (sample data)
 Models/        Entity classes with data annotations (User TPH: Admin / Client /
                Consultant, Category, Service, Availability, TimeOff, Appointment,
                Payment, Review, ChatMessage, CertificatePhoto, Notification,
                UserToken), ValidationAttributes (custom validators), PagedList
 Models/ViewModels/  View models with validation attributes
 Services/      Helper (hashing, sign-in, photo upload), EmailService,
                SlotService, NotificationService, CaptchaService
 Hubs/          AppHub (SignalR chat + notifications)
 Views/         Razor views per controller + Shared partials
 wwwroot/       css/site.css, js/ (site, validation, slot-picker, photo-editor,
                chat, notifications), images/, uploads/ (runtime photos)


-------------------------------------------------------------------------------
 6. LIBRARIES
-------------------------------------------------------------------------------
 NuGet : Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools,
         SixLabors.ImageSharp (photo resize), Stripe.net (Stripe payment API)
 Local : Bootstrap 5.3, jQuery, jQuery Validation + Unobtrusive (project template)
 CDN   : Bootstrap Icons, Google Fonts, SignalR JS client, Chart.js, Leaflet,
         qrcodejs, html5-qrcode
 Self-written (no library): captcha, photo editor, paging, slot calendar,
         custom validation attributes + client adapters, CSV import parser,
         .ics export, notification sound (Web Audio API)


-------------------------------------------------------------------------------
 7. BEFORE SUBMISSION
-------------------------------------------------------------------------------
 * Build > Clean Solution, close Visual Studio, then delete the bin, obj and
   .vs folders before zipping (keep ExpertLinkDb.mdf / .ldf and the Migrations
   folder).
 * Make sure the database contains the sample data you want to demonstrate.
