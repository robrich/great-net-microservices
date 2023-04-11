namespace Microservices.ShoppingCart.Config;

public static class DiagnosticsConfig
{
	public static string ServiceName { get; set; } = null!;
	public static ActivitySource ActivitySource { get; set; } = null!;
	public static Meter Meter { get; set; } = null!;
	public static Counter<long> OrderCreationCalculator { get; set; } = null!;
}
