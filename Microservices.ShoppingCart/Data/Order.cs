using System.ComponentModel.DataAnnotations.Schema;

namespace Microservices.ShoppingCart.Data;

public class Order
{
	[Key]
	public int Id { get; set; }
	public int ProductId { get; set; }
	public int UserId { get; set; }
	public double Quantity { get; set; }
	public double Price { get; set; }
	public double Subtotal { get; set; }
	public double Tax { get; set; }
	public double Total { get; set; }

	[ForeignKey(nameof(ProductId))]
	public Product Product { get; set; } = null!;
	[ForeignKey(nameof(UserId))]
	public User User { get; set; } = null!;
}
