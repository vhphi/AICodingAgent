namespace Sample;

public sealed class CustomerRepository
{
    private readonly List<Customer> _customers =
    [
        new Customer { Code = "C001", Name = "Alice", IsActive = true },
        new Customer { Code = "C002", Name = "Bob", IsActive = false }
    ];

    public IReadOnlyList<Customer> GetAll()
    {
        return _customers;
    }
}
