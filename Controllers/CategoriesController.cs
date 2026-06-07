using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;
using System.Linq;
using System.Threading.Tasks;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/categories")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            if (!await _context.Categories.AnyAsync())
            {
                var defaultCategories = new List<Category>
                {
                    new Category { Name = "Electronics", IconUrl = "smartphone" },
                    new Category { Name = "Vehicles", IconUrl = "directions_car" },
                    new Category { Name = "Property", IconUrl = "real_estate_agent" },
                    new Category { Name = "Furniture", IconUrl = "chair" },
                    new Category { Name = "Fashion", IconUrl = "checkroom" },
                    new Category { Name = "Sports", IconUrl = "fitness_center" },
                    new Category { Name = "Books", IconUrl = "menu_book" },
                    new Category { Name = "Jobs", IconUrl = "work" }
                };
                _context.Categories.AddRange(defaultCategories);
                await _context.SaveChangesAsync();
            }

            var categories = await _context.Categories
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.IconUrl,
                    c.ParentCategoryId
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
        {
            var category = new Category
            {
                Name = dto.Name,
                IconUrl = dto.IconUrl ?? string.Empty,
                ParentCategoryId = dto.ParentCategoryId
            };
            
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            
            return Ok(new { category.Id, category.Name, category.IconUrl, category.ParentCategoryId });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            
            return Ok();
        }
    }
}
