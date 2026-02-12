# ITIL v4 Monitoring and Event Management Practice

## Purpose

Systematically observe services and service components, and record and report selected changes of state identified as events.

## Key Concepts

**Event**: Change of state with significance for management of a service or configuration item

**Alert**: Notification that threshold has been reached or anomaly detected

**Monitoring**: Repeated observation of a system, service, or component

**Event Types**:
- **Informational**: Normal operation logged for audit (e.g., user login)
- **Warning**: Threshold approaching but service still operating (e.g., disk 80% full)
- **Exception**: Service degraded or failed (e.g., service unavailable, CPU 100%)

## Do's

✅ Monitor proactively before users report issues
✅ Set appropriate alert thresholds (not too sensitive)
✅ Auto-create incidents for critical events
✅ Use dashboards for real-time visibility
✅ Track trends to predict capacity needs
✅ Alert the right people at the right time

## Don'ts

❌ Generate alerts nobody reads (alert fatigue)
❌ Monitor without acting on data
❌ Set thresholds that cause false positives
❌ Ignore informational events—they're valuable for analysis

## Key Metrics

- Mean time to detect (MTTD) incidents
- Alert accuracy (true positives vs. false positives)
- % incidents detected by monitoring vs. users
- Event volume trends

## Cross-Links

- Incident Management: See 060_incident_management.md
- Problem Management: See 070_problem_management.md
