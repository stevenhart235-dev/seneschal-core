namespace Seneschal.AspNetCore;

/// <summary>
/// Declares that an ASP.NET Core endpoint or controller action requires a
/// Seneschal capability decision.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresCapabilityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RequiresCapabilityAttribute"/> class.
    /// </summary>
    /// <param name="capabilityId">The required capability identifier.</param>
    public RequiresCapabilityAttribute(string capabilityId)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException(
                "Capability id is required.",
                nameof(capabilityId));
        }

        CapabilityId = capabilityId;
    }

    /// <summary>
    /// Gets the required capability identifier.
    /// </summary>
    public string CapabilityId { get; }

    /// <summary>
    /// Gets or sets the optional environment sent to Seneschal.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the optional resource identifier sent to Seneschal.
    /// </summary>
    public string? ResourceId { get; set; }
}
