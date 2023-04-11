namespace Microservices.ShoppingCart.Data;

public class User
{
	[Key]
	public int Id { get; set; }
	public string ZipCode { get; set; } = "";
}
