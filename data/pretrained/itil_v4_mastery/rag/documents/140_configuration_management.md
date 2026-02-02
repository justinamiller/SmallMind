# ITIL v4 Service Configuration Management Practice

## Purpose

Ensure accurate and reliable information about configuration of services and supporting CIs is available when needed.

## Key Concepts

**Configuration Item (CI)**: Component managed to deliver a service (servers, applications, network devices, documentation)

**Configuration Management Database (CMDB)**: Repository of CI records and their relationships

**Configuration Baseline**: Documented configuration at specific point in time

## CMDB Relationships

- **Runs-on**: Application runs on server
- **Depends-on**: Service depends on database
- **Connects-to**: Server connects to network
- **Part-of**: Disk is part of server

## Do's

✅ Keep CMDB accurate and up-to-date
✅ Integrate CMDB with discovery tools
✅ Document CI relationships and dependencies
✅ Use CMDB for impact analysis (what breaks if this CI changes?)
✅ Version control configuration baselines
✅ Automate CI discovery where possible

## Don'ts

❌ Manually maintain CMDB (too error-prone)
❌ Let CMDB become stale
❌ Track everything as CI (focus on what matters)
❌ Create CMDB without governance

## Key Metrics

- CMDB accuracy (audit vs. actual)
- CI coverage (% of infrastructure tracked)
- Age of CI records (freshness)
- Unauthorized CI rate (shadow IT)

## Cross-Links

- Change Enablement: See 080_change_enablement.md
- IT Asset Management: See 050_practices_overview.md
