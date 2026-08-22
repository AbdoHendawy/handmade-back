using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Identity;

public sealed class Role : Entity
{
    private Role()
    {
    }

    private Role(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; } = string.Empty;

    public static Role Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.") { Code = "invalid_role" };
        }

        return new Role(CreateId(), name.Trim());
    }

    public static Role Create(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.") { Code = "invalid_role" };
        }

        return new Role(id, name.Trim());
    }
}
