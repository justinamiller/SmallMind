# ITIL v4 Service Value Chain

## Purpose

The Service Value Chain is the central component of the ITIL v4 Service Value System (SVS). It consists of six interconnected activities that organizations perform to create value through IT services. Understanding the value chain helps organizations design flexible, efficient workflows.

## The Six Value Chain Activities

### 1. Plan

**Definition**: Ensure shared understanding of vision, status, and improvement direction across all stakeholders

**Key Objectives**:
- Establish organizational strategy and portfolio direction
- Create service and product roadmaps
- Define architectures and policies
- Allocate resources and budgets
- Manage risks and compliance

**Inputs**: Policies, customer requirements, regulatory mandates, market opportunities  
**Outputs**: Strategic plans, architectural designs, portfolio decisions

**Practices Supporting Plan**:
- Strategy management
- Portfolio management
- Architecture management
- Financial management
- Risk management

**Example**: Annual IT strategy session defines cloud migration roadmap, prioritizes which services to modernize, allocates budget.

### 2. Improve

**Definition**: Ensure continual improvement of services, practices, and all organizational elements

**Key Objectives**:
- Identify improvement opportunities
- Assess current state and gaps
- Implement changes to increase value
- Measure effectiveness of improvements

**Inputs**: Performance data, customer feedback, audit findings, incidents  
**Outputs**: Improvement initiatives, updated practices, value stream optimizations

**Practices Supporting Improve**:
- Continual improvement
- Measurement and reporting  
- Organizational change management

**Example**: Post-incident review identifies root cause; improvement team implements automated monitoring to prevent recurrence.

### 3. Engage

**Definition**: Provide understanding of stakeholder needs, transparency, and continual engagement

**Key Objectives**:
- Understand customer and user needs
- Build relationships with stakeholders
- Manage demand and opportunities
- Provide service offerings via catalog
- Handle requests and feedback

**Inputs**: Customer contacts, market research, service requests  
**Outputs**: Requirements, demand forecasts, service catalog, engagement plans

**Practices Supporting Engage**:
- Relationship management
- Service desk
- Service catalog management
- Supplier management

**Example**: Service desk receives user request for new capability; relationship manager works with business to understand broader need; demand is captured for planning.

### 4. Design & Transition

**Definition**: Ensure services meet stakeholder expectations for quality, cost, and time-to-market

**Key Objectives**:
- Design new or changed services
- Transition services to live environments
- Validate services against requirements
- Ensure readiness for deployment

**Inputs**: Requirements, architectural standards, budget constraints  
**Outputs**: Service designs, tested components, transition plans, documentation

**Practices Supporting Design & Transition**:
- Service design
- Change enablement  
- Release management
- Service validation and testing
- Knowledge management

**Example**: Development team designs new API service; creates deployment runbook; conducts UAT; transitions to production via change process.

### 5. Obtain/Build

**Definition**: Ensure service components are available when needed and meet agreed specifications

**Key Objectives**:
- Source or develop service components
- Procure third-party solutions
- Build custom software or infrastructure
- Manage intellectual property

**Inputs**: Service designs, procurement policies, build specifications  
**Outputs**: Service components (software, infrastructure, contracts), acquired capabilities

**Practices Supporting Obtain/Build**:
- Software development and management
- Infrastructure and platform management
- Deployment management
- Supplier management

**Example**: Company needs CRM system; evaluates build vs. buy; procures SaaS solution; integrates with identity management.

### 6. Deliver & Support

**Definition**: Ensure services are delivered and supported according to agreed specifications

**Key Objectives**:
- Deliver services to users
- Respond to service requests
- Resolve incidents and service disruptions
- Monitor service health and performance
- Maintain service components

**Inputs**: User requests, incidents, monitoring alerts  
**Outputs**: Service delivered, incidents resolved, request fulfilled, health reports

**Practices Supporting Deliver & Support**:
- Incident management
- Service desk
- Service request management
- Problem management
- Monitoring and event management
- IT asset management

**Example**: User reports application outage; monitoring detected issue simultaneously; incident team restores service; post-incident analysis feeds improvement.

## How Activities Interconnect

The value chain is **non-linear**—activities can occur in any order or in parallel, depending on context:

- **Engage → Plan**: Customer demand informs strategic planning
- **Plan → Design & Transition**: Strategy drives service design priorities
- **Design & Transition → Obtain/Build**: Designs specify what to build or buy
- **Obtain/Build → Deliver & Support**: Components are deployed and supported
- **Deliver & Support → Improve**: Operational data reveals improvement needs
- **Improve → [Any Activity]**: Optimizations can target any part of the chain

**Example Value Stream**:
*New Feature Request*
1. **Engage**: Customer submits request via service desk
2. **Plan**: Product team prioritizes in backlog
3. **Design & Transition**: Developers design and code feature
4. **Obtain/Build**: Feature built in CI/CD pipeline
5. **Deliver & Support**: Feature deployed, monitored
6. **Improve**: Usage analytics suggest enhancements

## Value Streams vs. Value Chain

- **Value Chain**: Six universal activities (ITIL defines these)
- **Value Stream**: Specific sequences of activities for a particular scenario (organizations define these)

**Example**:
- Value Chain: Plan, Improve, Engage, Design & Transition, Obtain/Build, Deliver & Support (always these six)
- Value Stream: "Onboard New Employee" uses Engage → Obtain/Build → Deliver & Support
- Value Stream: "Deploy Software Release" uses Design & Transition → Obtain/Build → Deliver & Support → Improve

## Practical Do's

✅ **Map your value streams**: Identify the sequence of activities for common scenarios  
✅ **Measure flow**: Track how long work takes to move through activities  
✅ **Eliminate handoff waste**: Reduce delays between activities  
✅ **Enable parallel work**: Don't force sequential when parallel is possible  
✅ **Automate transitions**: Use CI/CD, orchestration to move work faster  
✅ **Staff all activities**: Ensure each activity has capable resources

## Practical Don'ts

❌ **Don't force linear flow**: Value chain is flexible, not waterfall  
❌ **Don't isolate activities**: Teams should collaborate across boundaries  
❌ **Don't ignore feedback loops**: Deliver & Support insights must reach Plan and Improve  
❌ **Don't create silos**: "Design team" and "Ops team" must integrate  
❌ **Don't skip activities**: All six are needed, even if some are lightweight

## Signals of Effective Value Chain

1. Work flows smoothly from activity to activity without bottlenecks
2. Teams understand which activities they contribute to
3. Value streams are documented and continuously optimized
4. Feedback from Deliver & Support reaches Plan and Improve quickly
5. Parallel activities occur when possible (e.g., build while designing)
6. Metrics show decreasing lead times for common value streams

## Anti-Patterns

🚫 **Waterfall Mentality**: Forcing sequential progression through all six activities  
🚫 **Silo Optimization**: Improving one activity at the expense of overall flow  
🚫 **Missing Feedback**: Deliver & Support team problems never reach Improve  
🚫 **Planning Paralysis**: Over-investing in Plan while starving Deliver  
�� **Build-Only Focus**: Neglecting Engage to understand what customers actually need

## Cross-Links

- **Service Value System**: See document 010_service_value_system.md
- **Practices Overview**: See document 050_practices_overview.md
- **Continual Improvement**: See document 160_continual_improvement.md

## Common Q&A

**Q: Must every service go through all six activities?**  
A: Conceptually yes, but some activities may be minimal. A simple user request might touch Engage → Deliver & Support lightly.

**Q: What's the difference between value chain and lifecycle?**  
A: ITIL v3 lifecycle was sequential (Strategy→Design→Transition→Operation). Value chain is flexible and non-linear.

**Q: Can activities happen simultaneously?**  
A: Yes! For example, one team might be in Deliver & Support for Service A while another designs Service B.

**Q: How do I map my workflows to the value chain?**  
A: Document your value streams (e.g., "Deploy code change"), then annotate which value chain activities occur at each step.

**Q: Do all six activities require dedicated teams?**  
A: Not necessarily. Small organizations may have one team performing multiple activities. Large enterprises often specialize.

**Q: How does the value chain relate to DevOps?**  
A: DevOps practices map well: Dev focuses on Design & Obtain/Build; Ops on Deliver & Support; both collaborate on Improve.

## Summary

The Service Value Chain provides six universal activities that enable value creation. Unlike rigid lifecycles, the chain is flexible—activities occur in different sequences depending on context. Organizations define specific value streams that use these activities in patterns suited to their services. Effective value chain management requires clear handoffs, minimal waste, fast feedback loops, and continuous optimization of flow.
