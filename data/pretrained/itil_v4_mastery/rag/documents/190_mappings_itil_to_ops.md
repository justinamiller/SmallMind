# ITIL v4 Mappings to Operations

## Purpose

Show how ITIL v4 practices map to real-world operational scenarios and modern delivery approaches.

## ITIL in DevOps Context

### Continuous Integration/Deployment
**ITIL Practices**: Change Enablement, Release Management, Deployment Management

**How They Fit**:
- Git commits = standard changes (pre-approved)
- Pull request review = lightweight change approval
- CI pipeline success = validation
- Automated deployments = release and deployment
- Feature flags = control deployment risk

### Site Reliability Engineering (SRE)
**ITIL Practices**: Incident Management, Problem Management, Service Level Management

**How They Fit**:
- Error budgets = quantified SLA flexibility
- Post-mortems = incident and problem PIR
- Toil reduction = automation, continual improvement
- Runbooks = knowledge management

### Cloud Operations
**ITIL Practices**: Capacity Management, Availability Management, Configuration Management

**How They Fit**:
- Auto-scaling = capacity management automation
- Multi-region deployment = availability and continuity
- Infrastructure as Code (IaC) = configuration baselines
- Cloud cost optimization = financial management

## ITIL in Traditional IT Context

### Ticketing and Service Desk
**ITIL Practices**: Service Desk, Incident Management, Service Request Management

**Operations**:
- ServiceNow/Jira for ticket tracking
- Multi-tier support (L1, L2, L3)
- SLA tracking dashboards
- Knowledge base integration

### Change Advisory Boards
**ITIL Practices**: Change Enablement, Risk Management

**Operations**:
- Weekly CAB meetings
- Risk assessment templates
- Change calendar coordination
- Approval workflows in ITSM tools

### Asset and Configuration Management
**ITIL Practices**: IT Asset Management, Configuration Management

**Operations**:
- CMDB maintained via discovery tools
- Asset lifecycle (procurement → deployment → retirement)
- License compliance tracking
- Dependency mapping for impact analysis

## Common Operational Workflows

### Onboarding New Employee
**Practices**: Service Request, IT Asset, Identity/Access, Knowledge

**Steps**:
1. HR triggers onboarding request
2. IT provisions laptop, phone, accounts
3. Access rights granted per role
4. New hire accesses knowledge base for setup guides

### Deploying Code to Production
**Practices**: Change Enablement, Release, Deployment, Monitoring

**Steps**:
1. Code passes automated tests (CI)
2. Release packaged with metadata
3. Change automatically approved (standard change)
4. Deployment via pipeline (canary/blue-green)
5. Monitoring validates health
6. Rollback if issues detected

### Responding to Outage
**Practices**: Monitoring, Incident, Communication, Problem

**Steps**:
1. Monitoring alerts trigger incident
2. On-call engineer paged
3. Incident declared, war room established
4. Status page updated
5. Service restored via runbook or escalation
6. Post-incident review identifies improvement
7. Problem ticket created for root cause analysis

### Quarterly Capacity Planning
**Practices**: Capacity Management, Financial Management, Measurement

**Steps**:
1. Review historical usage trends
2. Forecast based on business projections
3. Identify bottlenecks and constraints
4. Plan upgrades or cloud scaling
5. Budget for capacity additions
6. Implement and monitor

## ITIL Adoption by Organization Size

### Small (10-100 employees)
**Focus**: Incident, Service Desk, Change, Knowledge  
**Approach**: Lightweight, tool-assisted (Jira, Slack)  
**Staffing**: One person may cover multiple practices

### Medium (100-1000 employees)
**Focus**: Add Problem, SLA, Catalog, Monitoring  
**Approach**: Dedicated ITSM platform (ServiceNow, Freshservice)  
**Staffing**: Specialized roles (incident manager, problem analyst)

### Large (1000+ employees)
**Focus**: Full practice suite, governance, integration  
**Approach**: Enterprise ITSM platform, automation, cross-functional teams  
**Staffing**: Centers of Excellence, practice owners, maturity programs

## Cross-Links

- Guiding Principles: See 020_guiding_principles.md
- Service Value Chain: See 040_service_value_chain.md
- Practices Overview: See 050_practices_overview.md
