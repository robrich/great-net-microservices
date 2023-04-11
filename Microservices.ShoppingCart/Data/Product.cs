namespace Microservices.ShoppingCart.Data;

public class Product
{
	[Key]
	public int Id { get; set; }
	public double Price { get; set; }
}
