namespace Seneschal.AspNetCore;

/// <summary>
/// Controls how middleware applies decisions returned by Seneschal.
/// </summary>
public enum SeneschalEnforcementBehavior
{
    /// <summary>
    /// Honors the enforcement mode returned by Seneschal.
    /// Monitor/log-only responses are allowed through.
    /// </summary>
    HonorDecisionMode,

    /// <summary>
    /// Always allows the request through after evaluation.
    /// </summary>
    Monitor,

    /// <summary>
    /// Enforces the returned decision even if the runtime response is
    /// monitor/log-only.
    /// </summary>
    Enforce
}
