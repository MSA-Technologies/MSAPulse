namespace MSAPulse.TestApi.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public List<Order> Orders { get; set; } = new();
}