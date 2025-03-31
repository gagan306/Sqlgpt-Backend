using ChatApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ChatApi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Question> Questions { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    }
}