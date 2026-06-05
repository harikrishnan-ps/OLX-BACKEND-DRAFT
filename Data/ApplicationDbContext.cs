// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using olx_api.Models;

namespace olx_api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<ListingImage> ListingImages { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<State> States { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<StaticPage> StaticPages { get; set; }
        public DbSet<InAppNotification> InAppNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            modelBuilder.Entity<StaticPage>()
                .HasIndex(p => p.Slug)
                .IsUnique();

            // 1. Configure Composite Key for Favorites (Many-to-Many Bridge)
            modelBuilder.Entity<Favorite>()
                .HasKey(f => new { f.UserId, f.ListingId });

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.Listing)
                .WithMany(l => l.FavoritedBy)
                .HasForeignKey(f => f.ListingId)
                .OnDelete(DeleteBehavior.Restrict); // Prevents accidental cascading loops

            // 2. Configure Self-Referencing Category Hierarchy
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. Configure Dual-Relationship for Chat Messages
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Configure Double-sided Relationships for User Reviews
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Reviewer)
                .WithMany()
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.TargetUser)
                .WithMany()
                .HasForeignKey(r => r.TargetUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. Precision setup for decimal data types (Crucial for SQL Server)
            modelBuilder.Entity<Listing>()
                .Property(l => l.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Report>()
    .HasOne(r => r.Reporter)
    .WithMany()
    .HasForeignKey(r => r.ReporterId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.ReportedListing)
                .WithMany()
                .HasForeignKey(r => r.ReportedListingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<City>()
                .HasOne(c => c.State)
                .WithMany(s => s.Cities)
                .HasForeignKey(c => c.StateId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
