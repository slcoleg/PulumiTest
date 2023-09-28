using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.Tasks;
using System.Threading;

namespace infra_api;

public class SampleHealthCheck : IHealthCheck
{
  public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    var isHealthy = true;

    // ...

    if (isHealthy)
    {
      return Task.FromResult(
          HealthCheckResult.Healthy("A healthy result."));
    }

    return Task.FromResult(
        new HealthCheckResult(
            context.Registration.FailureStatus, "An unhealthy result."));
  }
}