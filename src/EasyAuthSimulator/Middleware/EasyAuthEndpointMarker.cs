namespace EasyAuthSimulator.Middleware;

/// <summary>
/// Endpoint metadata tag applied to every /.auth/* endpoint so
/// <see cref="GlobalValidationMiddleware"/> can exempt them from the unauthenticated-client
/// gate regardless of what <c>ApiPrefix</c> is configured to.
/// </summary>
public sealed class EasyAuthEndpointMarker;
