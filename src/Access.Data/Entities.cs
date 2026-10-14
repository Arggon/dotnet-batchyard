namespace Access.Data;

public sealed class Plan
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public List<Role> Roles { get; } = [];
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Permission is the domain term.")]
public sealed class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
}

public sealed class Role
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int? MaxBatchSize { get; set; }
    public List<Permission> Permissions { get; } = [];
}

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class ApiClient
{
    public string Id { get; set; } = "";
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = "";
    public string SecretHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<Assignment> Assignments { get; } = [];
}

public sealed class Assignment
{
    public const string FromPlan = "plan";
    public const string Manual = "manual";

    public string ClientId { get; set; } = "";
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string Source { get; set; } = FromPlan;
}

/// <summary>A row of dbo.Outbox; drained to RabbitMQ by the shared outbox dispatcher.</summary>
public sealed class OutboxRow
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? TraceParent { get; set; }
}
