using System.ComponentModel.DataAnnotations;

namespace olx_api.Models
{
    public class Listing
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public bool IsNegotiable { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? SpecificationsJson { get; set; }
        
        // UPDATED: Removed string State/City. Linked to relational DB City
        public int CityId { get; set; }
        public City City { get; set; }

        // NEW PROPERTIES BELOW
        public string Condition { get; set; } // "New", "Used"
        public string Status { get; set; } = "Active"; // Active, Draft, Sold, Pending, Rejected, Deleted
        public bool IsFeatured { get; set; } = false;
        public DateTime LastBoostedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAt { get; set; }

        // Foreign Keys & Navigation
        public Guid UserId { get; set; }
        public User User { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }

        public ICollection<ListingImage> Images { get; set; }
        public ICollection<Favorite> FavoritedBy { get; set; }
    }
}
