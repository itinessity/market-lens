# Verification notes

Verified on 11 September 2026 using .NET SDK 10.0.401, Node.js 22 and Chromium/Playwright 1.63.0 on Windows.

- Release compilation: passed, no warnings or errors.
- Domain/protocol executable: 6 checks passed.
- Isolated HTTP workflow checks: passed.
- Browser primary workflow and mobile overflow checks: 2 passed.
- Browser JavaScript errors during the live workflow: none observed.
- npm dependency audit: zero known vulnerabilities at verification time.

## Separate live-provider smoke check

Kraken WebSocket v2 reached live status with zero reconnects and zero checksum failures during the captured check. Both instruments loaded, 120 completed hourly candles rendered, a recording was saved and replayed, and the UI returned to live mode.

The screenshot was captured from the live-provider mode with English browser locale and reviewed visually. Provider availability and data may change after this check. CI uses explicitly labeled deterministic fixtures rather than external uptime.

Dockerfile and Compose configuration are included, but a Docker engine was not available on the verification machine; container execution was not tested locally. The Release .NET application was built and run directly. This repository has not been deployed as a public web service.
