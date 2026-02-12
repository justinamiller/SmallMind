# ITIL v4 Practices Overview

## Purpose

ITIL v4 defines 34 management practices—sets of organizational resources designed for performing work or accomplishing an objective. Practices replaced the ITIL v3 concept of "processes" to encompass broader capabilities including culture, technology, information, and people.

## What is a Practice?

A **practice** is more comprehensive than a process:
- **Process**: Specific workflow with defined inputs, outputs, activities
- **Practice**: Process + procedures + policies + roles + skills + tools + culture

## The 34 ITIL v4 Practices

### General Management Practices (14)

These apply across the organization, not just IT:

1. **Strategy Management**: Define organizational direction and approach
2. **Portfolio Management**: Decide which initiatives to invest in
3. **Architecture Management**: Provide understanding of structure and relationships
4. **Service Financial Management**: Track and optimize costs and value
5. **Workforce and Talent Management**: Ensure right people with right skills
6. **Continual Improvement**: Make incremental and breakthrough improvements
7. **Measurement and Reporting**: Support decision-making with data
8. **Risk Management**: Identify, assess, handle risks
9. **Information Security Management**: Protect information
10. **Knowledge Management**: Maintain and share knowledge
11. **Organizational Change Management**: Enable smooth adoption of changes
12. **Project Management**: Deliver changes via structured projects
13. **Relationship Management**: Build stakeholder connections
14. **Supplier Management**: Ensure supplier performance

### Service Management Practices (17)

These are specific to IT service management:

1. **Availability Management**: Ensure services meet availability needs
2. **Business Analysis**: Analyze business needs and recommend solutions
3. **Capacity and Performance Management**: Ensure adequate capacity
4. **Change Enablement**: Minimize risk of changes
5. **Incident Management**: Restore normal service quickly
6. **IT Asset Management**: Track and optimize assets
7. **Monitoring and Event Management**: Observe services and systems
8. **Problem Management**: Reduce incident likelihood and impact
9. **Release Management**: Make new/changed services available
10. **Service Catalog Management**: Provide accurate information about services
11. **Service Configuration Management**: Ensure accurate configuration information
12. **Service Continuity Management**: Minimize disaster impact
13. **Service Design**: Design fit-for-purpose services
14. **Service Desk**: Capture demand for incident resolution and service requests
15. **Service Level Management**: Set clear service targets
16. **Service Request Management**: Handle service requests
17. **Service Validation and Testing**: Ensure services meet requirements

### Technical Management Practices (3)

These support technical infrastructure:

1. **Deployment Management**: Move components to live environments
2. **Infrastructure and Platform Management**: Oversee technology infrastructure
3. **Software Development and Management**: Develop and maintain applications

## Practice Adoption Strategy

**Not all at once**: Few organizations implement all 34 practices simultaneously.

**Phased Approach**:

**Phase 1 (Foundation)**: Core operational practices
- Incident Management
- Service Desk
- Service Request Management
- Monitoring and Event Management

**Phase 2 (Stability)**: Minimize disruption
- Change Enablement
- Problem Management
- Knowledge Management

**Phase 3 (Proactive)**: Plan and optimize
- Capacity and Performance Management
- Availability Management
- Service Level Management

**Phase 4 (Strategic)**: Alignment and governance
- Service Catalog Management
- Portfolio Management
- Continual Improvement

**Phase 5 (Advanced)**: Comprehensive ITSM
- All remaining practices as needed

## Practice Relationships

Practices are interconnected:

**Example 1: Incident → Problem → Change**
- Incident Management: Resolves immediate outage
- Problem Management: Identifies root cause
- Change Enablement: Approves preventive fix
- Knowledge Management: Documents solution

**Example 2: Request → Catalog → Fulfillment**
- Service Catalog: Lists available services
- Service Desk: Receives request
- Service Request Management: Fulfills request
- IT Asset Management: Provisions asset

**Example 3: Monitoring → Incident → Escalation**
- Monitoring and Event Management: Detects anomaly
- Incident Management: Triages and resolves
- Service Level Management: Tracks against SLA
- Relationship Management: Communicates with stakeholders

## Do's for Practice Implementation

✅ **Start with pain points**: Adopt practices where problems are most acute  
✅ **Integrate practices**: Design how they handoff and share information  
✅ **Define ownership**: Assign clear accountability for each practice  
✅ **Provide training**: Ensure practitioners understand their roles  
✅ **Use appropriate tooling**: Select ITSM platforms that support workflows  
✅ **Measure outcomes**: Track value delivered, not just compliance

## Don'ts for Practice Implementation

❌ **Don't implement in isolation**: Practices must integrate  
❌ **Don't ignore culture**: Training alone won't change behavior  
❌ **Don't copy-paste templates**: Customize to your context  
❌ **Don't mandate all 34**: Implement what adds value  
❌ **Don't forget governance**: Without authority, practices aren't followed

## Signals of Mature Practice Adoption

1. Practices integrate seamlessly (e.g., incidents trigger problems automatically)
2. Clear ownership and escalation for each practice
3. Tooling supports workflows without manual workarounds
4. Metrics show improving outcomes (faster resolution, fewer failures)
5. Staff trained and competent in their practice areas
6. Continuous improvement is part of normal operations

## Anti-Patterns

🚫 **Checkbox Compliance**: Implementing practices "because ITIL says so" without understanding value  
🚫 **Process Silos**: Incident team unaware of what problem team does  
🚫 **Tool-Driven**: Buying ITSM platform then forcing workflows to match tool defaults  
🚫 **Documentation Theater**: Writing 100-page practice docs nobody reads  
🚫 **Ignoring Culture**: Assuming process documents will change behavior

## Cross-Links

- **Service Value Chain**: See document 040_service_value_chain.md
- **Incident Management**: See document 060_incident_management.md
- **Problem Management**: See document 070_problem_management.md
- **Change Enablement**: See document 080_change_enablement.md

## Common Q&A

**Q: Must I implement all 34 practices?**  
A: No. Implement practices that address your needs. Most organizations start with 5-10 core practices.

**Q: What's the difference between practice and process?**  
A: Practices encompass processes plus people, tools, culture, and governance. Processes are one component of practices.

**Q: Which practices should I implement first?**  
A: Focus on operational stability first: Incident, Service Desk, Change, Monitoring. Add others based on pain points.

**Q: Can practices work with Agile/DevOps?**  
A: Yes. Many practices (Change Enablement, Release, Deployment) explicitly support Agile and DevOps workflows.

**Q: How do I know if a practice is working?**  
A: Measure outcomes. Incident Management: Mean time to resolve (MTTR). Change: Failure rate. Problem: Repeat incident reduction.

## Summary

ITIL v4's 34 practices provide comprehensive capabilities for service management. Organizations should adopt practices incrementally based on value and need—not implement all at once. Practices must integrate to support end-to-end workflows. Success requires ownership, training, appropriate tools, and continual improvement.
