#:package dottype.Build@0.2.0
#:property dottype=./customer.json --name Customer

var customer = new Customer { id = 42, name = "Ada" };
Console.WriteLine($"{customer.id}: {customer.name}");
