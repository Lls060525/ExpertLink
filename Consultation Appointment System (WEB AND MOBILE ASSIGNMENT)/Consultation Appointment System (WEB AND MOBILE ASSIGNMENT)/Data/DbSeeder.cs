using ExpertLink.Models;
using ExpertLink.Services;

namespace ExpertLink.Data;

// Inserts sample data for demonstration the first time the database is created.
public static class DbSeeder
{
    public static void Seed(AppDbContext db, Helper hp)
    {
        if (db.Users.Any()) return;

        var rnd = new Random(2026);
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        // ------------------------------------------------------------ Categories
        var cats = new List<Category>
        {
            new() { Name = "Legal Advice", Icon = "bi-bank", Description = "Contracts, tenancy, family law, employment disputes and more." },
            new() { Name = "Financial Planning", Icon = "bi-cash-coin", Description = "Budgeting, investments, retirement, insurance and tax planning." },
            new() { Name = "Career Coaching", Icon = "bi-briefcase", Description = "Resume reviews, interview preparation and career transitions." },
            new() { Name = "Mental Wellness", Icon = "bi-heart-pulse", Description = "Counselling for stress, anxiety, relationships and personal growth." },
            new() { Name = "Business Consulting", Icon = "bi-graph-up-arrow", Description = "Start-up advice, business plans, marketing and operations." },
            new() { Name = "IT & Tech Consulting", Icon = "bi-cpu", Description = "Software architecture, cloud, cybersecurity and digital transformation." },
            new() { Name = "Education Counselling", Icon = "bi-mortarboard", Description = "University applications, scholarships and study pathways." },
        };
        db.Categories.AddRange(cats);

        // ------------------------------------------------------------ Users
        string adminHash = hp.HashPassword("Admin@123");
        string consHash = hp.HashPassword("Consult@123");
        string clientHash = hp.HashPassword("Client@123");

        db.Admins.Add(new Admin { Name = "System Administrator", Email = "admin@expertlink.com", Phone = "012-3456789", Hash = adminHash, EmailVerified = true, CreatedAt = now.AddMonths(-8) });
        db.Admins.Add(new Admin { Name = "Nurul Aisyah", Email = "aisyah.admin@expertlink.com", Phone = "013-2223344", Hash = adminHash, EmailVerified = true, CreatedAt = now.AddMonths(-6) });

        var consultants = new List<Consultant>
        {
            NewConsultant("Tan Wei Ming", "consultant@expertlink.com", cats[0], "Advocate & Solicitor", "LLB (Hons) University of Malaya, CLP", 12,
                "Practising lawyer with 12 years of experience in civil litigation, tenancy disputes and employment law. I explain legal options in plain language so you can make informed decisions.",
                "Level 12, Menara Hap Seng, Jalan P. Ramlee, Kuala Lumpur", 3.1529, 101.7066,
                [("Initial Legal Consultation", 30, 80m, true, true), ("Contract Review Session", 60, 200m, true, true), ("Tenancy Dispute Advice", 60, 150m, false, true)]),
            NewConsultant("Priya Ramasamy", "priya@expertlink.com", cats[1], "Licensed Financial Planner", "CFP, BBA Finance (Monash)", 9,
                "Helping young professionals and families build wealth through sensible budgeting, EPF and PRS planning, unit trusts and insurance reviews.",
                "Unit 8-3, The Vertical, Bangsar South, Kuala Lumpur", 3.1100, 101.6650,
                [("Personal Budget Review", 45, 90m, true, true), ("Retirement & EPF Planning", 60, 180m, true, true), ("Personal Tax Filing Help", 30, 60m, true, false)]),
            NewConsultant("Jason Lim", "jason@expertlink.com", cats[2], "Certified Career Coach", "MSc HRM, ICF ACC", 7,
                "Former tech recruiter turned career coach. I help graduates and mid-career professionals land interviews and negotiate better offers.",
                "Jalan SS 15/4, Subang Jaya, Selangor", 3.0738, 101.5886,
                [("Resume & LinkedIn Review", 45, 70m, true, false), ("Mock Interview", 60, 120m, true, true)]),
            NewConsultant("Dr. Farah Hassan", "farah@expertlink.com", cats[3], "Clinical Psychologist", "PhD Clinical Psychology (UKM)", 11,
                "Compassionate, evidence-based counselling for stress, anxiety, burnout and relationship issues in a safe and confidential setting.",
                "Wisma Medivest, Jalan Ampang, Kuala Lumpur", 3.1590, 101.7230,
                [("Counselling Session", 60, 160m, true, true), ("Stress Management Workshop (1-to-1)", 90, 220m, false, true)]),
            NewConsultant("Ahmad Zulkifli", "ahmad@expertlink.com", cats[4], "SME Business Advisor", "MBA (UiTM), SME Corp Registered Consultant", 15,
                "Fifteen years advising Malaysian SMEs on business plans, grant applications, pricing strategy and expansion.",
                "Kompleks Kenari, Jalan Kenari, Puchong, Selangor", 3.0230, 101.6160,
                [("Business Health Check", 60, 250m, true, true), ("Grant Application Advice", 45, 150m, true, true)]),
            NewConsultant("Chong Mei Ling", "meiling@expertlink.com", cats[5], "Cloud Solutions Architect", "BCS (Hons) TAR UMT, AWS Solutions Architect Pro", 8,
                "I help businesses move to the cloud securely and cost-effectively, and mentor developers on system design.",
                "Cyberjaya, Selangor", 2.9213, 101.6559,
                [("Cloud Migration Consultation", 60, 300m, true, false), ("System Design Mentoring", 60, 150m, true, false), ("Cybersecurity Quick Audit", 90, 400m, true, true)]),
            NewConsultant("Rajesh Kumar", "rajesh@expertlink.com", cats[6], "Education Counsellor", "MEd (USM)", 6,
                "Guidance for SPM/STPM leavers and parents on choosing courses, universities, scholarships and PTPTN.",
                "Jalan Burma, George Town, Penang", 5.4200, 100.3200,
                [("Study Pathway Consultation", 45, 60m, true, true), ("Scholarship Application Review", 60, 100m, true, false)]),
            NewConsultant("Siti Hajar Yusof", "sitihajar@expertlink.com", cats[1], "Islamic Finance Advisor", "BSc Islamic Banking (IIUM), CIF", 5,
                "Shariah-compliant financial planning: takaful, home financing, zakat and investment planning.",
                "Shah Alam, Selangor", 3.0733, 101.5185,
                [("Takaful & Protection Review", 45, 80m, true, true), ("Home Financing Advice", 60, 120m, true, true)]),
        };
        db.Consultants.AddRange(consultants);

        // A consultant application awaiting admin approval
        var pending = NewConsultant("Kevin Ong", "kevin@expertlink.com", cats[2], "Leadership Coach", "BA Psychology, Certified NLP Practitioner", 4,
            "Coaching first-time managers on leadership, communication and team building.", null, null, null,
            [("Leadership Coaching", 60, 130m, true, false)]);
        pending.IsApproved = false;
        pending.CreatedAt = now.AddDays(-2);
        db.Consultants.Add(pending);

        var clients = new List<Client>
        {
            new() { Name = "Lee Jia Hui", Email = "client@expertlink.com", Phone = "011-23456789" },
            new() { Name = "Muhammad Hafiz", Email = "hafiz@example.com", Phone = "017-6543210" },
            new() { Name = "Kavitha Nair", Email = "kavitha@example.com", Phone = "016-7788990" },
            new() { Name = "Ng Chee Keong", Email = "ckng@example.com", Phone = "012-9988776" },
            new() { Name = "Aina Sofea", Email = "aina@example.com", Phone = "019-3344556" },
            new() { Name = "Daniel Wong", Email = "daniel@example.com", Phone = "018-1122334" },
            new() { Name = "Amirul Hakim", Email = "amirul@example.com", Phone = "014-5566778" },
        };
        foreach (var c in clients)
        {
            c.Hash = clientHash;
            c.EmailVerified = true;
            c.CreatedAt = now.AddDays(-rnd.Next(30, 200));
        }
        clients.Add(new Client { Name = "Unverified User", Email = "unverified@example.com", Phone = "012-0000000", Hash = clientHash, EmailVerified = false, CreatedAt = now.AddDays(-1) });
        db.Clients.AddRange(clients);

        foreach (var c in consultants.Append(pending))
        {
            c.Hash = consHash;
            c.EmailVerified = true;
        }

        db.SaveChanges();

        // ------------------------------------------------------------ Appointments
        var appts = new List<Appointment>();
        var methods = new[] { "Card", "FPX", "E-Wallet" };
        var concerns = new[]
        {
            "I would like some advice on my current situation.",
            "Need guidance before making an important decision.",
            "Follow-up from my previous session.",
            "Looking for a second opinion.",
            "Want to understand my options and next steps.",
            null
        };
        var comments = new[]
        {
            "Very professional and easy to understand. Highly recommended!",
            "Gave me clear, practical steps. Worth every ringgit.",
            "Friendly and patient, answered all my questions.",
            "Good session, though it ran a little short.",
            "Extremely knowledgeable. Will book again.",
            "Helpful advice, I feel much more confident now.",
            null
        };

        bool Clash(Consultant con, Client cli, DateOnly d, TimeOnly s, TimeOnly e) =>
            appts.Any(a => a.Date == d && s < a.EndTime && e > a.StartTime && (a.Consultant == con || a.Client == cli));

        Appointment Make(Client cli, Consultant con, Service svc, DateOnly d, TimeOnly s, AppointmentStatus st)
        {
            var mode = svc.AllowOnline && (!svc.AllowInPerson || rnd.Next(2) == 0) ? ConsultationMode.Online : ConsultationMode.InPerson;
            var created = d.ToDateTime(s).AddDays(-rnd.Next(2, 12));
            if (created > now) created = now.AddHours(-rnd.Next(1, 24));
            var a = new Appointment
            {
                Client = cli, Consultant = con, Service = svc, Date = d, StartTime = s,
                EndTime = s.AddMinutes(svc.DurationMinutes), Mode = mode, Fee = svc.Fee, Status = st,
                CreatedAt = created, Concern = concerns[rnd.Next(concerns.Length)]
            };
            if (st != AppointmentStatus.Pending && st != AppointmentStatus.Rejected && st != AppointmentStatus.Cancelled && mode == ConsultationMode.Online)
                a.MeetingLink = $"https://meet.google.com/{Code(3)}-{Code(4)}-{Code(3)}";
            a.Payment = new Payment
            {
                Amount = svc.Fee,
                Method = methods[rnd.Next(methods.Length)],
                ReferenceNo = "PAY" + created.ToString("yyMMddHHmmss") + rnd.Next(100, 999),
                CardLast4 = rnd.Next(1000, 9999).ToString(),
                PaidAt = created.AddMinutes(3),
                Status = st is AppointmentStatus.Cancelled or AppointmentStatus.Rejected ? PaymentStatus.Refunded : PaymentStatus.Paid,
            };
            if (a.Payment.Method != "Card") a.Payment.CardLast4 = null;
            if (a.Payment.Status == PaymentStatus.Refunded) a.Payment.RefundedAt = created.AddDays(1);
            if (st == AppointmentStatus.Cancelled) a.CancelReason = "Schedule conflict, will rebook later.";
            if (st == AppointmentStatus.Rejected) a.CancelReason = "Sorry, I am attending a court hearing at this time.";
            if (st is AppointmentStatus.Completed or AppointmentStatus.CheckedIn) a.CheckedInAt = d.ToDateTime(s).AddMinutes(-rnd.Next(0, 10));
            if (st == AppointmentStatus.Completed)
                a.SessionNotes = "Discussed the client's concerns in detail, explained available options and agreed on next steps. Follow-up recommended in 4-6 weeks.";
            appts.Add(a);
            return a;
        }

        string Code(int n) => new string(Enumerable.Range(0, n).Select(_ => (char)('a' + rnd.Next(26))).ToArray());

        DateOnly NextWeekday(DateOnly d)
        {
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
            return d;
        }

        // Guaranteed demo appointments for the main client / main consultant
        var mainClient = clients[0];
        var mainCons = consultants[0];
        var d1 = NextWeekday(today.AddDays(1));
        var demoConfirmed = Make(mainClient, mainCons, mainCons.Services[1], d1, new TimeOnly(10, 0), AppointmentStatus.Confirmed);
        Make(mainClient, consultants[1], consultants[1].Services[0], NextWeekday(today.AddDays(3)), new TimeOnly(14, 0), AppointmentStatus.Pending);
        Make(clients[1], mainCons, mainCons.Services[0], NextWeekday(today.AddDays(2)), new TimeOnly(9, 0), AppointmentStatus.Pending);
        Make(clients[2], mainCons, mainCons.Services[2], d1, new TimeOnly(15, 0), AppointmentStatus.Confirmed);
        // Today: one confirmed in-person session (QR check-in demo) and one already checked in (complete demo)
        int nextHour = Math.Clamp(now.Hour + 1, 9, 22);
        var todayQr = Make(mainClient, mainCons, mainCons.Services[0], today, new TimeOnly(nextHour, 0), AppointmentStatus.Confirmed);
        todayQr.Mode = ConsultationMode.InPerson;
        todayQr.MeetingLink = null;
        var todayCheckedIn = Make(clients[3], mainCons, mainCons.Services[0], today, new TimeOnly(8, 0), AppointmentStatus.CheckedIn);
        todayCheckedIn.Mode = ConsultationMode.InPerson;
        todayCheckedIn.MeetingLink = null;

        var pastNoReview = Make(mainClient, consultants[2], consultants[2].Services[1], NextWeekday(today.AddDays(-9)), new TimeOnly(11, 0), AppointmentStatus.Completed);
        _ = pastNoReview;

        var hours = new[] { 9, 10, 11, 14, 15, 16 };
        for (int i = 0; i < 160; i++)
        {
            var con = consultants[rnd.Next(consultants.Count)];
            var svc = con.Services[rnd.Next(con.Services.Count)];
            var date = today.AddDays(rnd.Next(-180, 22));
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || date == today) continue;
            var start = new TimeOnly(hours[rnd.Next(hours.Length)], 0);
            var end = start.AddMinutes(svc.DurationMinutes);
            if ((start.Hour < 12 && end > new TimeOnly(12, 0)) || end > new TimeOnly(17, 0)) continue;
            var cli = clients[rnd.Next(clients.Count - 1)];   // exclude unverified user
            if (Clash(con, cli, date, start, end)) continue;

            AppointmentStatus st;
            int r = rnd.Next(100);
            if (date < today)
                st = r < 80 ? AppointmentStatus.Completed : r < 88 ? AppointmentStatus.Cancelled : r < 94 ? AppointmentStatus.NoShow : AppointmentStatus.Rejected;
            else
                st = r < 65 ? AppointmentStatus.Confirmed : r < 90 ? AppointmentStatus.Pending : AppointmentStatus.Cancelled;

            var a = Make(cli, con, svc, date, start, st);
            if (st == AppointmentStatus.Completed && rnd.Next(100) < 75)
            {
                int rating = rnd.Next(100) switch { < 55 => 5, < 85 => 4, < 95 => 3, _ => 2 };
                a.Review = new Review
                {
                    Rating = rating,
                    Comment = comments[rnd.Next(comments.Length)],
                    CreatedAt = a.StartDateTime.AddHours(rnd.Next(2, 48)),
                    Reply = rnd.Next(3) == 0 ? "Thank you for your kind feedback!" : null
                };
            }
        }

        db.Appointments.AddRange(appts);
        db.SaveChanges();

        // ------------------------------------------------------------ Chat + notifications
        db.ChatMessages.AddRange(
            new ChatMessage { AppointmentId = demoConfirmed.Id, SenderId = mainClient.Id, Content = "Hi, should I bring a copy of the contract to our session?", SentAt = now.AddHours(-5) },
            new ChatMessage { AppointmentId = demoConfirmed.Id, SenderId = mainCons.Id, Content = "Yes please, a softcopy is fine too. See you then!", SentAt = now.AddHours(-4) }
        );
        db.Notifications.AddRange(
            new Notification { UserId = mainClient.Id, Message = $"Your appointment {demoConfirmed.Reference} has been confirmed.", Url = $"/Appointment/Details/{demoConfirmed.Id}", CreatedAt = now.AddHours(-6) },
            new Notification { UserId = mainCons.Id, Message = "You have new booking requests awaiting confirmation.", Url = "/Appointment?status=Pending", CreatedAt = now.AddHours(-3) }
        );
        db.SaveChanges();
    }

    private static Consultant NewConsultant(string name, string email, Category cat, string title, string qual, int years,
        string bio, string? address, double? lat, double? lng,
        (string Name, int Minutes, decimal Fee, bool Online, bool InPerson)[] services)
    {
        var c = new Consultant
        {
            Name = name, Email = email, Phone = "012-" + (1000000 + Math.Abs(name.Length * 7919 + years * 104729) % 9000000),
            Category = cat, Title = title, Qualification = qual, YearsExperience = years, Bio = bio,
            OfficeAddress = address, Latitude = lat, Longitude = lng, IsApproved = true,
            CreatedAt = DateTime.Now.AddMonths(-7)
        };
        foreach (var s in services)
        {
            c.Services.Add(new Service
            {
                Name = s.Name, DurationMinutes = s.Minutes, Fee = s.Fee, AllowOnline = s.Online, AllowInPerson = s.InPerson,
                Description = $"A {s.Minutes}-minute session: {s.Name.ToLower()} with {name}."
            });
        }
        // Mon-Fri 09:00-12:00 and 14:00-17:00, Saturday morning for some
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            c.Availabilities.Add(new Availability { Day = day, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) });
            c.Availabilities.Add(new Availability { Day = day, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(17, 0) });
        }
        if (years % 2 == 0)
            c.Availabilities.Add(new Availability { Day = DayOfWeek.Saturday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(13, 0) });
        return c;
    }
}
