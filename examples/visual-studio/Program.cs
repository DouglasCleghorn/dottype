using System.Text.Json;
using Models;

using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "customer.json"));
var customer = JsonSerializer.Deserialize<Customer>(input)!;
Console.WriteLine($"{customer.name} lives in {customer.address.city}.");

foreach (string line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "orders.ndjson")))
{
    var order = JsonSerializer.Deserialize<Order>(line)!;
    if (order.customerId == customer.id)
        Console.WriteLine($"Order {order.id} ships to {order.address.postal}.");
}
