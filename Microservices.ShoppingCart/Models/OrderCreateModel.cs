using System.ComponentModel.DataAnnotations.Schema;

namespace Microservices.ShoppingCart.Data;

public class OrderCreateModel
{
	public int ProductId { get; set; }
	[Range(0, 1000)]
	public double Quantity { get; set; }
}
