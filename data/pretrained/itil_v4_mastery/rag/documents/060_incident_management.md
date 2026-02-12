# ITIL v4 Incident Management Practice

## Purpose

Minimize negative impact of incidents by restoring normal service operation as quickly as possible. An incident is an unplanned interruption or reduction in quality of a service.

## Key Concepts

**Incident**: Unplanned interruption or quality reduction  
**Normal Service**: Service operating within agreed SLA parameters  
**Workaround**: Temporary solution that restores service (may not address root cause)  
**Major Incident**: Significant business impact requiring urgent attention

## Objectives

- Restore service quickly (minimize user impact and downtime)
- Log and track all incidents for analysis
- Provide timely communication to stakeholders
- Escalate when needed
- Contribute data to problem management

## Incident Management Workflow

1. **Detection and Logging**
   - User reports via service desk, email, phone
   - Automated monitoring triggers alert
   - Log: timestamp, user, symptoms, classification

2. **Categorization and Prioritization**
   - Category: Hardware, software, network, access
   - Impact: How many users/services affected
   - Urgency: How time-sensitive is resolution
   - Priority = f(Impact, Urgency)

3. **Initial Diagnosis**
   - Service desk attempts first-line resolution
   - Check known error database for existing solutions
   - Search knowledge base for similar incidents

4. **Escalation (if needed)**
   - Functional: Route to specialist team (network, database, security)
   - Hierarchical: Elevate to manager for resources or decisions
   - Major incident: Invoke major incident procedure

5. **Investigation and Resolution**
   - Specialist team diagnoses issue
   - Implements fix or workaround
   - Tests that service is restored
   - Documents actions taken

6. **Recovery and Closure**
   - Verify user confirms resolution
   - Update incident record with resolution details
   - Close incident
   - Optional: Trigger problem management if underlying cause unknown

7. **Post-Incident Activities**
   - Major incident review
   - Trend analysis to identify patterns
   - Knowledge article creation
   - Feedback to problem management

## Priority Matrix Example

| Impact \ Urgency | High Urgency | Medium Urgency | Low Urgency |
|-----------------|--------------|----------------|-------------|
| **High Impact** | P1 (Critical) | P2 (High) | P3 (Medium) |
| **Med Impact** | P2 (High) | P3 (Medium) | P4 (Low) |
| **Low Impact** | P3 (Medium) | P4 (Low) | P5 (Plan) |

**P1 (Critical)**: Target resolution < 4 hours  
**P2 (High)**: Target resolution < 24 hours  
**P3 (Medium)**: Target resolution < 72 hours  
**P4 (Low)**: Target resolution < 1 week  
**P5 (Planned)**: Schedule for next maintenance window

## Major Incident Management

**Triggers**: Executive escalation, widespread outage, data breach, revenue-impacting

**Special Procedures**:
- Dedicated major incident manager (MIM) coordinates
- War room or bridge call established
- Regular stakeholder updates (every 30-60 min)
- All hands on deck—pull resources as needed
- Expedited change approval for emergency fixes
- Mandatory post-incident review

## Do's for Incident Management

✅ **Log everything**: Even quick fixes—patterns emerge from data  
✅ **Communicate proactively**: Stakeholders want updates, even if "still investigating"  
✅ **Focus on restoration**: Get service back first, root cause analysis later  
✅ **Use known errors**: Don't reinvent solutions for recurring issues  
✅ **Escalate early**: If beyond your expertise, route immediately  
✅ **Document resolution**: Capture what worked for knowledge base  
✅ **Review major incidents**: Always conduct post-incident analysis

## Don'ts for Incident Management

❌ **Don't troubleshoot root cause**: That's problem management's job (unless trivial)  
❌ **Don't delay communication**: "No news" creates anxiety  
❌ **Don't skip logging**: Verbal resolution without record loses institutional knowledge  
❌ **Don't blame people**: Focus on restoring service, blame games waste time  
❌ **Don't implement risky changes**: Emergency changes still need approval  
❌ **Don't close prematurely**: Verify user confirms resolution

## Key Roles

**Service Desk Agent**: First-line responder, initial diagnosis, escalation  
**Incident Manager**: Oversees process, coordinates complex incidents  
**Major Incident Manager**: Leads major incident response  
**Technical Specialists**: Second/third-line support (DBAs, network engineers, developers)  
**Service Owner**: Accountable for service availability, approves emergency changes

## Key Metrics (KPIs)

1. **Mean Time to Resolve (MTTR)**: Average time from incident logged to resolved
2. **Mean Time to Respond (MTTR)**: Average time from logged to first response
3. **First-Call Resolution Rate**: % resolved by service desk without escalation
4. **Reopened Incidents**: % of incidents reopened (indicates premature closure)
5. **Incident Volume Trend**: Total incidents over time (should decrease with problem management)
6. **SLA Compliance**: % incidents resolved within target time
7. **Major Incident Frequency**: Count and duration of major incidents

**Targets**:
- MTTR: Decrease by 10% year-over-year
- First-call resolution: >70%
- Reopened rate: <5%
- SLA compliance: >95%

## Incident vs. Service Request vs. Problem

| Aspect | Incident | Service Request | Problem |
|--------|----------|----------------|---------|
| **Nature** | Unplanned disruption | Planned user need | Underlying cause |
| **Goal** | Restore service quickly | Fulfill request | Prevent recurrence |
| **Urgency** | Often high | Varies | Lower (proactive) |
| **Example** | "Email down" | "Need software license" | "Why does email crash monthly?" |

## Handoffs to Other Practices

- **Problem Management**: Recurring or unresolved incidents trigger problem investigation
- **Change Enablement**: Emergency fixes require expedited change approval
- **Knowledge Management**: Resolution details become knowledge articles
- **Service Level Management**: Incident data tracks SLA compliance
- **Continual Improvement**: Incident trends identify improvement opportunities

## Common Pitfalls (Anti-Patterns)

🚫 **Root Cause Hunting**: Spending hours diagnosing when workaround exists  
🚫 **Communication Silence**: Users escalate because nobody updates them  
🚫 **Escalation Hesitation**: Agent struggles alone instead of routing to expert  
🚫 **Blame Games**: Finger-pointing during outage instead of fixing  
🚫 **Knowledge Hoarding**: Resolutions not documented, same issue solved repeatedly  
🚫 **Tool Workarounds**: Using email/Slack instead of logging in ITSM tool

## Signals of Success

1. MTTR trending downward over time
2. Major incidents have documented runbooks
3. Knowledge base reduces duplicate investigations
4. Users report satisfaction with communication and speed
5. Few incidents reopen due to incomplete fixes
6. Service desk resolves majority without escalation

## Integration with Tools

- **ITSM Platform**: ServiceNow, Jira Service Management, Remedy
- **Monitoring**: Splunk, Datadog, Prometheus → auto-create incidents
- **Collaboration**: Slack, Teams for war rooms and updates
- **Knowledge Base**: Confluence, SharePoint for solutions
- **Runbooks**: PagerDuty, Rundeck for automated response

## Cross-Links

- **Problem Management**: See document 070_problem_management.md
- **Change Enablement**: See document 080_change_enablement.md
- **Service Request Management**: See document 090_service_request_management.md
- **Monitoring and Event Management**: See document 120_monitoring_and_event_management.md
- **Knowledge Management**: See document 150_knowledge_management.md

## Common Q&A

**Q: Should we troubleshoot root cause during incidents?**  
A: Only if it's quick and leads to permanent fix. Otherwise, restore service (workaround) and hand off to problem management.

**Q: How do we handle incidents reported multiple ways (email, phone, chat)?**  
A: Create single incident record, link all channels. Respond via preferred channel but centralize tracking.

**Q: What if users bypass service desk and email the team directly?**  
A: Politely redirect to service desk for logging. Explain that logging ensures tracking and accountability.

**Q: Should developers handle their own service incidents?**  
A: Service desk triages; complex issues escalate to developers. On-call rotations work for DevOps teams.

**Q: How long should we keep incident records?**  
A: Minimum 1 year for analysis. Archive older records. Some regulations require longer retention.

**Q: What's the difference between incident priority and severity?**  
A: Priority = Impact + Urgency (determines response time). Severity = technical criticality (informational).

## Practical Guidance

**For Service Desk Agents**:
- Use knowledge base before escalating
- Communicate clearly and empathetically
- Document thoroughly—your notes help next responder
- When in doubt, escalate early

**For Incident Managers**:
- Monitor queue for bottlenecks and aging tickets
- Coordinate major incidents calmly
- Ensure communication happens
- Facilitate post-incident reviews without blame

**For Leadership**:
- Fund adequate staffing for on-call rotations
- Invest in monitoring to detect before users report
- Reward blameless post-mortems
- Track metrics to identify improvement needs

## Summary

Incident Management focuses on rapid restoration of normal service. It's not root cause analysis (that's problem management) or fulfilling planned requests (that's service request management). Success requires clear prioritization, effective escalation, proactive communication, thorough logging, and integration with knowledge management. The goal: minimize user impact and prevent repeat incidents through continuous improvement.
