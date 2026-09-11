# Security policy

This is a single-instance local portfolio demonstration. It binds to loopback by default and does not include authentication, tenant isolation or production rate limiting. Before hosting it publicly, add authentication where saved records are exposed, HTTPS, request quotas, storage retention and operational monitoring.

Do not submit sensitive information to a shared instance. Report security issues through a private contact on https://itinessity.com rather than publishing sensitive details in an issue. Never include credentials in a report.

The application uses public provider endpoints without API keys. Test fixtures are available only when both `ASPNETCORE_ENVIRONMENT=Testing` and `TestFixtures=true` are set. A provider failure is never silently replaced by test data.
