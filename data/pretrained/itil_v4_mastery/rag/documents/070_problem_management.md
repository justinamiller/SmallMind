# ITIL v4 Problem Management Practice

## Purpose

Reduce the likelihood and impact of incidents by identifying actual and potential causes of incidents and managing workarounds and known errors.

## Key Concepts

**Problem**: The cause of one or more incidents  
**Known Error**: A problem that has been analyzed and has documented workaround  
**Workaround**: Temporary way to restore service without permanently fixing root cause  
**Root Cause**: The fundamental reason a problem exists

## Objectives

- Prevent incidents from happening
- Eliminate recurring incidents
- Minimize impact of incidents that cannot be prevented
- Identify patterns and trends
- Improve overall service quality

## Problem Management vs. Incident Management

| Aspect | Incident Management | Problem Management |
|--------|--------------------|--------------------|
| **Focus** | Restore service quickly | Find and fix root cause |
| **Timeline** | Immediate (hours/days) | Longer-term (days/weeks) |
| **Trigger** | Service disruption | Recurring/severe incidents or proactive analysis |
| **Success** | Service restored | Root cause eliminated |
| **Example** | "Get email working now" | "Why does email crash weekly?" |

## Problem Management Workflow

### Reactive Problem Management

Triggered by incidents:

1. **Problem Identification**
   - Multiple similar incidents
   - Single high-impact incident
   - Trending incident patterns
   - Log problem record

2. **Problem Analysis**
   - Gather incident data and logs
   - Reproduce issue in test environment
   - Use root cause analysis techniques (5 Whys, Ishikawa, Pareto)
   - Identify underlying cause

3. **Workaround Documentation**
   - If root cause fix is complex, document workaround
   - Create known error record
   - Share workaround with incident management and service desk

4. **Root Cause Resolution**
   - Design permanent fix
   - Submit RFC (Request for Change)
   - Implement via change management
   - Validate fix in production

5. **Problem Closure**
   - Verify incidents no longer occur
   - Update known error database
   - Close problem record
   - Document lessons learned

### Proactive Problem Management

Prevent problems before they cause incidents:

1. **Trend Analysis**
   - Analyze incident patterns (time, component, user group)
   - Use monitoring data to spot anomalies
   - Review change failure trends

2. **Risk Assessment**
   - Identify single points of failure
   - Assess emerging technology risks
   - Review vendor security advisories

3. **Preventive Action**
   - Implement redundancy for critical components
   - Apply patches before vulnerabilities are exploited
   - Capacity upgrades before saturation

## Root Cause Analysis Techniques

### 5 Whys
Ask "why" repeatedly to drill down to root cause:
- **Problem**: Website crashed
- **Why?** Database connection pool exhausted
- **Why?** Too many concurrent users
- **Why?** Marketing campaign drove unexpected traffic
- **Why?** Capacity planning didn't include marketing in forecasting
- **Root Cause**: No cross-department capacity planning process

### Ishikawa (Fishbone) Diagram
Categorize potential causes:
- **People**: Lack of training
- **Process**: No capacity monitoring
- **Technology**: Database config limits
- **Environment**: Cloud region saturation

### Pareto Analysis
80% of incidents come from 20% of problems—focus on high-impact causes.

## Do's for Problem Management

✅ **Separate from incident management**: Different teams/focus  
✅ **Involve specialists**: Bring in DBAs, architects, developers  
✅ **Use data**: Analyze logs, metrics, incident history  
✅ **Document known errors**: Share workarounds widely  
✅ **Prioritize by impact**: Focus on problems causing most pain  
✅ **Measure prevention**: Track incidents prevented, not just solved  
✅ **Collaborate with vendors**: External products may require vendor engagement

## Don'ts for Problem Management

❌ **Don't rush to "fix"**: Ensure you understand root cause first  
❌ **Don't blame individuals**: Focus on process/system failures  
❌ **Don't hoard knowledge**: Known errors must be accessible  
❌ **Don't solve in isolation**: Involve change management for fixes  
❌ **Don't ignore workarounds**: Temporary solutions have value while root cause analysis proceeds  
❌ **Don't let known errors linger**: Prioritize permanent fixes

## Key Roles

**Problem Manager**: Oversees practice, prioritizes problems, owns known error database  
**Technical Specialists**: Perform analysis (DBAs, developers, network engineers)  
**Problem Analyst**: Investigates specific problems  
**Change Manager**: Approves fixes submitted as RFCs  
**Service Owner**: Accountable for service quality improvement

## Key Metrics (KPIs)

1. **Problem Resolution Time**: Average time from problem logged to root cause fixed
2. **Recurring Incident Reduction**: % decrease in repeat incidents post-fix
3. **Known Error Database Size**: Number of documented workarounds
4. **Known Error Age**: How long known errors remain unresolved
5. **Proactive vs. Reactive Ratio**: % problems identified before causing incidents
6. **Incidents Prevented**: Estimated incidents avoided via proactive fixes
7. **Problem Backlog**: Number of open problems

**Targets**:
- 60% reduction in recurring incidents year-over-year
- <10% of known errors older than 6 months
- 30% of problems identified proactively (vs. reactive)

## Handoffs to Other Practices

- **Incident Management**: Triggers reactive problem analysis
- **Change Enablement**: Root cause fixes submitted as RFCs
- **Knowledge Management**: Known errors documented in knowledge base
- **Continual Improvement**: Problem trends inform improvement initiatives
- **Service Level Management**: Problem resolution improves SLA compliance

## Common Pitfalls (Anti-Patterns)

🚫 **Symptom Fixing**: Treating symptoms instead of root causes  
🚫 **Analysis Paralysis**: Spending months analyzing without implementing fixes  
🚫 **Known Error Graveyard**: Hundreds of documented workarounds never permanently fixed  
🚫 **Blame Culture**: Focusing on who broke it instead of what broke  
🚫 **Siloed Analysis**: Problem analysts working without incident or operations data  
🚫 **Duplicate Efforts**: Multiple teams investigating same root cause independently

## Signals of Success

1. Incident volume decreasing over time
2. Repeat incidents becoming rare
3. Known error database actively used by service desk
4. Root cause fixes implemented within reasonable time
5. Major incidents trigger mandatory problem analysis
6. Trending and proactive analysis prevent incidents
7. Cross-functional problem-solving teams

## Integration with Tools

- **ITSM Platform**: Problem records linked to related incidents
- **Log Analysis**: Splunk, ELK stack for pattern detection
- **Monitoring**: Trend analysis from Datadog, Prometheus
- **Collaboration**: Wiki/Confluence for known errors
- **RCA Tools**: Cause mapping software, decision trees

## Problem Prioritization Matrix

| Frequency \ Impact | High Impact | Med Impact | Low Impact |
|-------------------|-------------|------------|-----------|
| **Very Frequent** | P1 (Urgent) | P2 (High) | P3 (Med) |
| **Frequent** | P2 (High) | P3 (Med) | P4 (Low) |
| **Infrequent** | P3 (Med) | P4 (Low) | P5 (Monitor) |

**P1 (Urgent)**: Immediate investigation, target fix within 30 days  
**P2 (High)**: Investigation within 1 week, fix within quarter  
**P3 (Med)**: Address in next planning cycle  
**P4 (Low)**: Document workaround, fix opportunistically  
**P5 (Monitor)**: Track, may become higher priority if frequency increases

## Cross-Links

- **Incident Management**: See document 060_incident_management.md
- **Change Enablement**: See document 080_change_enablement.md
- **Knowledge Management**: See document 150_knowledge_management.md
- **Continual Improvement**: See document 160_continual_improvement.md
- **Monitoring and Event Management**: See document 120_monitoring_and_event_management.md

## Common Q&A

**Q: Should every incident trigger a problem?**  
A: No. Focus on recurring incidents, high-impact incidents, or patterns. One-off minor incidents don't warrant problem investigation.

**Q: Can incident and problem management be the same team?**  
A: Possible in small organizations, but separate focus is better. Incident teams are firefighting; problem teams need analytical time.

**Q: How long should a known error remain unfixed?**  
A: Depends on impact and complexity. High-impact should be fixed within quarter. Low-impact may persist longer if workaround is effective.

**Q: What if root cause is in vendor software?**  
A: Document as known error, engage vendor support, implement workaround. Track vendor fix timeline.

**Q: Should we create problems for planned improvements?**  
A: No. Use continual improvement or change management for enhancements. Problems address causation of incidents.

**Q: How do we measure incidents prevented?**  
A: Estimate based on historical frequency. Example: Problem caused 20 incidents/month; after fix, 0 incidents = 240 prevented annually.

## Practical Guidance

**For Problem Managers**:
- Review incident queue daily for patterns
- Prioritize ruthlessly—can't fix everything
- Ensure known errors are documented and accessible
- Push for fixes, not just workarounds
- Communicate problem status regularly

**For Technical Analysts**:
- Reproduce issues in test environments
- Use data, not assumptions
- Involve operations staff—they know the systems
- Document analysis process for future reference
- Don't get stuck in analysis—timebox investigations

**For Leadership**:
- Staff problem management separately from incident management
- Fund root cause fixes in change budget
- Incentivize prevention, not just firefighting
- Review known error age and backlog
- Celebrate incident prevention wins

## Summary

Problem Management prevents and eliminates incidents by addressing root causes. It works hand-in-hand with incident management—incidents get service back; problems keep service stable. Effective problem management requires dedicated analytical resources, collaboration across teams, data-driven root cause analysis, timely implementation of fixes via change management, and a culture that values prevention over reactive firefighting.
