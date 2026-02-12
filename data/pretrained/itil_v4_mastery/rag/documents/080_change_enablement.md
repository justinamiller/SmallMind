# ITIL v4 Change Enablement Practice

## Purpose

Maximize the number of successful changes by ensuring risks are properly assessed, authorization is obtained, and changes are scheduled effectively. Previously called "Change Management" in ITIL v3, the name change emphasizes enabling business velocity, not gatekeeping.

## Key Concepts

**Change**: Addition, modification, or removal of anything that could affect IT services  
**Request for Change (RFC)**: Formal proposal to implement a change  
**Change Authority**: Person or group authorized to approve changes  
**Change Advisory Board (CAB)**: Group that assesses and recommends on changes  
**Change Schedule**: Plan showing when changes will occur  
**Change Window**: Pre-approved time period for implementing changes  
**Post-Implementation Review (PIR)**: Assessment of whether change achieved objectives

## Objectives

- Enable beneficial changes quickly and safely
- Minimize disruption from changes
- Optimize risk vs. velocity tradeoff
- Ensure changes are properly authorized
- Learn from both successful and failed changes

## Change Types

### Standard Changes
**Definition**: Pre-authorized, low-risk, well-understood changes with documented procedure

**Examples**:
- Password resets
- Routine server patching
- Standard software installations
- Authorized hardware refreshes

**Approval**: Pre-authorized, no RFC needed for each occurrence  
**Lead Time**: Minimal (often automated)

### Normal Changes
**Definition**: Changes requiring evaluation and authorization before implementation

**Examples**:
- Application upgrades
- Infrastructure changes
- New service deployments
- Architectural modifications

**Approval**: Requires RFC, review, and appropriate authorization  
**Lead Time**: Days to weeks, depending on risk and scope

### Emergency Changes
**Definition**: Changes required urgently to restore service or mitigate security risk

**Examples**:
- Hotfixes for critical security vulnerabilities
- Emergency restores from backup
- Urgent configuration changes during outages

**Approval**: Expedited process, may use Emergency CAB (ECAB)  
**Lead Time**: Hours (authorization happens quickly or retrospectively)

## Change Enablement Workflow

1. **Create RFC**
   - Requester documents: description, business justification, scope, timeline
   - Includes: implementation plan, backout plan, testing approach
   - Identifies: affected services, configuration items, risks

2. **Assess and Authorize**
   - **Low Risk**: Change manager approves
   - **Medium Risk**: Peer review or technical authority approves
   - **High Risk**: CAB reviews and recommends, change authority approves
   - **Emergency**: ECAB or change manager with post-approval review

3. **Schedule**
   - Coordinate with business cycles (avoid tax season, Black Friday)
   - Assign to appropriate change window (maintenance window, low-traffic periods)
   - Ensure dependencies are addressed (other changes, resource availability)

4. **Implement**
   - Implementer follows documented plan
   - Communications sent to stakeholders
   - Real-time monitoring for issues
   - Backout triggered if failures occur

5. **Review (PIR)**
   - Verify objectives were met
   - Assess whether risks materialized
   - Document lessons learned
   - Update change model if applicable

## Change Assessment Criteria

**Risk Evaluation** considers:
- **Service Impact**: How critical is affected service?
- **Scope**: How many users/systems affected?
- **Complexity**: How many moving parts?
- **Novelty**: Have we done this before successfully?
- **Timing**: Business-sensitive period?

**Risk Level** determines:
- **Authority Required**: Who must approve
- **CAB Review**: Needed or not
- **Testing Rigor**: How extensive
- **Backout Plan**: Mandatory or optional
- **Change Window**: Scheduled or emergency

## Change Advisory Board (CAB)

**Purpose**: Provide expert assessment and recommendations for changes

**Composition**:
- Change Manager (chairs meeting)
- Service owners of affected services
- Technical representatives (infrastructure, applications, security)
- Business representatives
- Optional: Customer representatives, vendors

**Meeting Cadence**: Weekly for normal changes, ad-hoc for emergencies

**CAB Activities**:
- Review RFC details
- Assess risk and impact
- Identify conflicts with other changes
- Recommend approval, deferral, or rejection
- Change authority makes final decision

**CAB Don'ts**:
❌ Become a rubber stamp (approve everything)  
❌ Become a bottleneck (reject everything)  
❌ Operate without data (demand metrics on change success)  
❌ Ignore business context (balance risk with value)

## Do's for Change Enablement

✅ **Right-size approval**: Standard changes shouldn't need CAB review  
✅ **Enable automation**: CI/CD pipelines can incorporate lightweight change approval  
✅ **Measure change success rate**: Track failures and learn from them  
✅ **Schedule changes wisely**: Respect business cycles and peak usage  
✅ **Require backout plans**: Every change should be reversible  
✅ **Communicate changes**: Notify affected stakeholders before/during/after  
✅ **Review failures**: PIR is mandatory for failed changes

## Don'ts for Change Enablement

❌ **Don't block business velocity**: Change process should enable, not prevent  
❌ **Don't skip backout plans**: Hope is not a strategy  
❌ **Don't ignore standard change criteria**: Repetitive changes should be pre-approved  
❌ **Don't rubber-stamp emergencies**: Even urgent changes need risk assessment  
❌ **Don't forget communication**: Surprise changes erode trust  
❌ **Don't implement without testing**: Especially in production

## Key Roles

**Change Manager**: Owns change process, chairs CAB, ensures compliance  
**Change Authority**: Approves changes (varies by risk level)  
**Change Implementer**: Executes the change (developer, engineer, DBA)  
**Change Requester**: Initiates RFC (could be anyone)  
**CAB Members**: Assess risk, provide expertise, recommend decision  
**Service Owner**: Accountable for service; must approve changes affecting their service

## Key Metrics (KPIs)

1. **Change Success Rate**: % of changes implemented without failure or backout
2. **Emergency Change %**: Ratio of emergency to total changes (should be low)
3. **Unauthorized Change %**: % of changes bypassing process (shadow IT)
4. **Change Lead Time**: Average time from RFC to implementation
5. **Failed Change Rate**: % requiring backout or causing incidents
6. **PIR Completion Rate**: % of changes with documented post-implementation review
7. **Change Calendar Accuracy**: % of changes implemented on schedule

**Targets**:
- Success rate: >95% for normal changes, >98% for standard changes
- Emergency changes: <5% of total
- Unauthorized changes: <2%
- Failed change rate: <5%
- PIR completion: 100% for failed changes, >80% for all changes

## Integration with DevOps and Agile

**ITIL v4 explicitly supports modern delivery**:

- **Continuous Deployment**: Standard changes with automated approval
- **Feature Flags**: Changes deployed dark, enabled via low-risk toggle
- **Canary Releases**: Phased rollouts minimize blast radius
- **Automated Rollbacks**: Pre-authorized backout procedures
- **Peer Review**: Pull request approval = change authorization for low-risk code

**Key Principle**: Match process rigor to risk. High-frequency, low-risk changes (code commits) use lightweight approval. Infrequent, high-risk changes (database schema migrations) use traditional CAB review.

## Handoffs to Other Practices

- **Release Management**: Packages changes into releases
- **Deployment Management**: Executes change implementation
- **Incident Management**: Failed changes may cause incidents
- **Problem Management**: Recurring change failures trigger root cause analysis
- **Configuration Management**: Changes update CI records in CMDB

## Common Pitfalls (Anti-Patterns)

🚫 **Change Prevention Board**: CAB rejects most changes out of risk aversion  
🚫 **Bureaucratic Overhead**: 40-page RFC for routine changes  
🚫 **Emergency Addiction**: Everything is "urgent" to bypass process  
🚫 **Unauthorized Changes**: DevOps teams ignore change process entirely  
🚫 **No Backout Plans**: Failed changes leave systems in broken state  
🚫 **Stale Change Calendar**: Schedule not updated, conflicts occur

## Signals of Success

1. High change success rate (>95%)
2. Emergency changes declining over time
3. Standard changes well-defined and automated
4. Change calendar accurate and trusted
5. Failed changes trigger PIR and improvements
6. DevOps teams participate (not bypass)
7. Business sees IT as enabler, not blocker

## Cross-Links

- **Release Management**: See document 130_release_management.md
- **Incident Management**: See document 060_incident_management.md
- **Problem Management**: See document 070_problem_management.md
- **Configuration Management**: See document 140_configuration_management.md

## Common Q&A

**Q: Does every code commit need a change ticket?**  
A: No. Routine code commits can be standard changes if they follow defined process (peer review, automated testing, deployment pipeline).

**Q: Can we implement emergency changes without approval?**  
A: Implement first to restore service, but get retrospective approval and conduct PIR. Don't let "emergency" become the norm.

**Q: How do we handle vendor-managed SaaS changes?**  
A: Vendor changes are often beyond your control. Focus on assessing impact, communicating to users, and coordinating your own configuration changes.

**Q: What's the difference between change and release?**  
A: Change is the approval and execution. Release is the packaging and distribution of changes (often bundles multiple changes).

**Q: Should CAB meet weekly if changes are rare?**  
A: Adapt cadence. If changes are infrequent, meet on-demand. If frequent, weekly or bi-weekly.

**Q: How do we reduce change lead time?**  
A: Pre-approve standard changes, streamline RFC templates, delegate low-risk approvals, use automation.

## Practical Guidance

**For Change Managers**:
- Monitor change success rate and investigate failures
- Identify candidates for standard change designation
- Ensure CAB meetings are efficient (30-60 min max)
- Communicate change calendar broadly
- Review emergency change trends

**For CAB Members**:
- Come prepared (read RFCs in advance)
- Ask clarifying questions, don't just vote
- Consider business value, not just risk
- Recommend risk mitigation, don't just reject
- Respect meeting time limits

**For Change Implementers**:
- Document implementation and backout plans thoroughly
- Test changes in non-prod environments
- Communicate status during implementation
- Conduct PIR honestly, even if change succeeded
- Learn from failures

**For Leadership**:
- Measure change success rate and velocity
- Fund automation to reduce manual change overhead
- Don't punish failed changes—incentivize learning
- Balance governance with agility
- Empower change authorities to make decisions

## Summary

Change Enablement ensures changes are properly assessed, authorized, and implemented to maximize success and minimize disruption. The practice balances risk management with business velocity—it should enable changes, not prevent them. Effective change enablement uses risk-based approval (standard, normal, emergency), clear authorization levels, integrated tooling, thorough planning, and post-implementation review to drive continual improvement. In modern DevOps environments, change processes must adapt to high-frequency deployments while maintaining appropriate governance for high-risk changes.
