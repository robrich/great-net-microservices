namespace Microservices.ShoppingCart.Data;

public class ShoppingCartDbContext : DbContext
{
	public ShoppingCartDbContext(DbContextOptions<ShoppingCartDbContext> options) : base(options)
	{
	}

	public DbSet<User> Users => Set<User>();
	public DbSet<Order> Orders => Set<Order>();
	public DbSet<Product> Products => Set<Product>();
}
