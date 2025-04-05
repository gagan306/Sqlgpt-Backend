using ChatApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ChatApi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Question> Questions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Email)
                .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.Token)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
   
}