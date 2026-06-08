# Verification Status

Verified locally on this machine:

| Stack | Projects | Verification |
| --- | --- | --- |
| .NET | `platform-control-plane`, `dotnet-incident-runbook-automation` | `dotnet run` test runners passed |
| Node / Frontend / MERN / Ecommerce / QA | `market-data-command-center`, `commerce-webhook-reliability`, `storefront-health-monitor`, `financial-ui-component-library`, `portfolio-analytics-bff`, `realtime-order-management-api`, `financial-test-automation-framework`, `api-contract-testing-harness` | `npm test` passed |
| Java | `java-trade-event-processor`, `java-payment-settlement-api` | `javac` compile and test mains passed |
| PHP | `laravel-billing-domain`, `php-coupon-discount-engine` | `php` test scripts passed |
| IT Support | `endpoint-compliance-automation`, `m365-onboarding-runbook` | PowerShell scripts executed successfully |

Not locally verified because the runtime is not installed in this workspace:

| Stack | Projects | Needed |
| --- | --- | --- |
| Go | `go-market-data-router`, `go-settlement-reconciler` | Go toolchain |
| Python / Data / Healthcare | `python-payment-orchestration-service`, `fraud-detection-pipeline`, `data-observability-pipeline`, `care-authorization-platform`, `pharmacy-revenue-cycle-workflow` | Python 3.x, pytest, optional Airflow/dbt |
| Ruby on Rails | `rails-subscription-billing`, `rails-marketplace-payouts` | Ruby and Rails/Bundler |

The unverified projects still contain implementation-ready source files and README instructions, but they should be converted into fully runnable projects once the required runtimes are available.
