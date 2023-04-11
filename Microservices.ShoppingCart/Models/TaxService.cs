namespace Microservices.ShoppingCart.Models;

public class TaxServiceRequest
{
	public double InvoiceSubtotal { get; set; }
	public string ZipCode { get; set; } = string.Empty;
}
public class TaxServiceResponse
{
	public double TaxAmount { get; set; }
	public double InvoiceTotal { get; set; }
}
