# Production Hardening

## Required before production

- Set a unique strong JWT signing key using a secret manager.
- Set PostgreSQL credentials outside source control.
- Change/remove the demo admin account.
- Use HTTPS end-to-end.
- Put the API behind a reverse proxy or load balancer.
- Configure database backups and recovery testing.
- Use EF Core migrations for controlled schema changes.
- Configure a real email provider for notification delivery.
- Review CORS policy before exposing the API to a separate frontend origin.

## Operational endpoints

- `GET /health` — application/database health check.
- `GET /ready` — readiness check that verifies database connectivity.

## Rate limiting

API controllers use a fixed-window limit of 120 requests per minute per application instance. Tune this for the final deployment topology.
