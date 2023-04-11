namespace Microservices.ShoppingCart.Controllers
{
    [ApiController]
	[Route("shoppingcart/[controller]")]
	public class OrderController : ControllerBase
	{
		private readonly ShoppingCartDbContext db;
		private readonly HttpClient taxService;
		private readonly ILogger<OrderController> logger;

		public OrderController(ShoppingCartDbContext db, IHttpClientFactory httpClientFactory, ILogger<OrderController> logger) {
			this.db = db ?? throw new ArgumentNullException(nameof(db));
			ArgumentNullException.ThrowIfNull(httpClientFactory);
			this.taxService = httpClientFactory.CreateClient("TaxService");
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		[HttpGet("{id}")]
		public IActionResult Get(int id, int userid/*TODO from JWT*/)
		{
			User? user = db.Users.Find(userid);
			if (user == null)
			{
				return Unauthorized(new { message = "Please login" });
			}
			Order? order = (
				from o in db.Orders
				where o.Id == id
				&& o.UserId == userid
				select o
			).FirstOrDefault();
			return order != null ? Ok(order) : NotFound();
		}

		[HttpPost("")]
		public async Task<IActionResult> Create(OrderCreateModel model, int userid/*TODO from JWT*/)
		{
			// Validate data
			User? user = db.Users.Find(userid);
			if (user == null)
			{
				return Unauthorized(new { message = "Please login" });
			}
			Product? product = db.Products.Find(model.ProductId);
			if (product == null)
			{
				this.ModelState.AddModelError(nameof(model.ProductId), "product not found");
			}
			if (!this.ModelState.IsValid || product == null)
			{
				return BadRequest(this.ModelState);
			}

			// Build order
			Order order = new Order
			{
				ProductId = product.Id,
				UserId = userid,
				Quantity = model.Quantity,
				Price = product.Price,
				Subtotal = Math.Round(product.Price * model.Quantity, 2),
			};

			// Get tax data
			TaxServiceResponse? taxdata = null;
			using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("GetTax", ActivityKind.Server))
			{
				activity?.AddTag("Request", JsonSerializer.Serialize(model));
				activity?.AddBaggage("UserId", user.Id.ToString());

				var res = await taxService.PostAsJsonAsync("/tax/calculate", new TaxServiceRequest { InvoiceSubtotal = order.Subtotal, ZipCode = user.ZipCode });
				if (!res.IsSuccessStatusCode)
				{
					logger.Log(LogLevel.Error, $"Error getting tax for zip {user.ZipCode}, subtotal {order.Subtotal}: {res.StatusCode}");
					return BadRequest(new { message = "Error calculating tax. Try your request again later." });
				}
				taxdata = await res.Content.ReadFromJsonAsync<TaxServiceResponse>();
				if (taxdata == null)
				{
					logger.Log(LogLevel.Error, $"Error deserializing tax response for zip {user.ZipCode}, subtotal {order.Subtotal}: {res.StatusCode}");
					return BadRequest(new { message = "Error gathering tax. Try your request again later." });
				}
			}

			order.Tax = taxdata.TaxAmount;
			order.Total = taxdata.InvoiceTotal;

			// Save order
			using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("SaveOrder", ActivityKind.Server))
			{
				db.Orders.Add(order);
				db.SaveChanges();
				activity?.AddTag("OrderId", order.Id.ToString());
			}
			DiagnosticsConfig.OrderCreationCalculator.Add(1, new KeyValuePair<string, object?>("OrderId", order.Id));

			// TODO: publish order to service bus

			// Return results
			string url = Url.Action(nameof(Get), new { id = order.Id, userid = userid })!;
			return Created(url, order);
		}
	}
}
