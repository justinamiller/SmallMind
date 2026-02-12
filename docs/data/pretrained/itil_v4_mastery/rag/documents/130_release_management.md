# ITIL v4 Release Management Practice

## Purpose

Make new and changed services and features available for use.

## Key Concepts

**Release**: Version of service or service component made available for use

**Release Package**: Set of configuration items (hardware, software, documentation) tested and deployed together

**Release Types**:
- **Major Release**: Significant new functionality
- **Minor Release**: Enhancements, small features
- **Emergency Release**: Urgent fix for critical issue

## Do's

✅ Bundle related changes into planned releases
✅ Test releases in non-production environments
✅ Communicate release schedules to stakeholders
✅ Provide rollback plans
✅ Coordinate with change management
✅ Automate deployment where possible (CI/CD)

## Don'ts

❌ Release untested changes to production
❌ Surprise users with unannounced releases
❌ Deploy during business-critical periods without approval
❌ Skip documentation updates

## Key Metrics

- Release frequency
- Release success rate
- Rollback rate
- Deployment lead time (commit to production)

## Cross-Links

- Change Enablement: See 080_change_enablement.md
- Deployment Management: See 050_practices_overview.md
