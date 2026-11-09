using ExpertLink.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Data;

// Code-first DbContext. Tables/columns are defined with DATA ANNOTATIONS on the
// entity classes (Models folder) - no Fluent API is used.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // User hierarchy (TPH -> single Users table)
    public DbSet<User> Users => Set<User>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Consultant> Consultants => Set<Consultant>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<TimeOff> TimeOffs => Set<TimeOff>();
    public DbSet<CertificatePhoto> CertificatePhotos => Set<CertificatePhoto>();

    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
}
