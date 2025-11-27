using Microsoft.EntityFrameworkCore;
using MSAPulse.TestApi.Models;

namespace MSAPulse.TestApi.Data;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; } 

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Laptop", Price = 1500 },
            new Product { Id = 2, Name = "Mouse", Price = 25 },
            new Product { Id = 3, Name = "Keyboard", Price = 50 }
        );

        modelBuilder.Entity<Order>().HasData(
            new Order { Id = 1, ProductId = 1, CustomerName = "Ahmet Yılmaz", OrderDate = DateTime.Now },
            new Order { Id = 2, ProductId = 1, CustomerName = "Ayşe Demir", OrderDate = DateTime.Now.AddDays(-1) },
            new Order { Id = 3, ProductId = 2, CustomerName = "Mehmet Can", OrderDate = DateTime.Now }
        );
    }
}