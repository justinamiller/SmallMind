# ITIL v4 Guiding Principles

## Purpose

The Seven Guiding Principles are universal recommendations that can guide organizations in all circumstances, regardless of changes in goals, strategies, type of work, or management structure. They represent core tenets for successful IT service management.

## The Seven Guiding Principles

### 1. Focus on Value

**Definition**: Everything the organization does should link back, directly or indirectly, to value for stakeholders.

**In Practice**:
- Understand who your stakeholders are (customers, users, employees, suppliers, partners)
- Know what they consider valuable (business outcomes, user experience, cost savings)
- Don't deliver features that nobody wants or uses
- Question activities that don't contribute to value

**Do**:
✅ Start every initiative by asking "What value does this create?"  
✅ Measure outcomes (customer satisfaction, revenue impact) not just outputs (tickets closed)  
✅ Involve customers in service design to ensure value alignment  
✅ Eliminate waste—work that doesn't create value

**Don't**:
❌ Build technology for technology's sake  
❌ Assume you know what customers value without asking  
❌ Focus solely on internal efficiency at the expense of customer experience

**Example**: Before implementing a new monitoring tool, ask: Will this reduce customer-impacting incidents? Will it help deliver services faster? If not, what value does it create?

### 2. Start Where You Are

**Definition**: Don't build something new without considering what's already available.

**In Practice**:
- Assess current state before designing solutions
- Reuse existing services, processes, tools, people, and knowledge
- Decide what to keep, what to improve, and what to retire
- Avoid "rip and replace" when evolution will work

**Do**:
✅ Inventory existing capabilities before purchasing new tools  
✅ Leverage institutional knowledge and established workflows  
✅ Measure current performance to identify real gaps  
✅ Start small—pilot improvements in one area before rolling out enterprise-wide

**Don't**:
❌ Declare "Everything is broken, we need to start over"  
❌ Ignore valuable existing assets and knowledge  
❌ Assume new tools will solve cultural or process problems

**Example**: Before buying a new ITSM platform, catalog what your current tools already provide. Often, 80% of requirements are met; focus on the 20% gap.

### 3. Progress Iteratively with Feedback

**Definition**: Resist the temptation to do everything at once. Organize work into smaller, manageable sections that can be completed in a timely manner.

**In Practice**:
- Use iterative approaches (sprints, phases, pilots)
- Gather feedback early and often
- Adjust based on lessons learned
- Deliver value incrementally rather than waiting for perfection

**Do**:
✅ Break large initiatives into 2-4 week iterations  
✅ Ship minimum viable products (MVPs) to get real feedback  
✅ Conduct retrospectives after each iteration  
✅ Pivot when feedback indicates a better path

**Don't**:
❌ Spend 12 months building before showing stakeholders  
❌ Ignore early warning signs that an approach isn't working  
❌ Wait for 100% perfection before releasing value

**Example**: Rather than implementing all ITIL practices at once, start with incident management, measure results for 3 months, gather feedback, refine, then add problem management.

### 4. Collaborate and Promote Visibility

**Definition**: Work across boundaries to produce results with greater buy-in, relevance, and likelihood of long-term success.

**In Practice**:
- Engage stakeholders early and continuously
- Break down silos between teams
- Make information visible and accessible
- Foster open communication and psychological safety

**Do**:
✅ Include representatives from all affected teams in planning  
✅ Share dashboards and metrics openly  
✅ Use collaborative tools (shared docs, chat, wikis)  
✅ Celebrate cross-team successes publicly

**Don't**:
❌ Make decisions in isolation then mandate compliance  
❌ Hoard information or keep data siloed  
❌ Blame individuals when things go wrong (learn from failures)

**Example**: When implementing change management, include developers, operations, security, and business stakeholders in designing the workflow—don't let ops dictate unilaterally.

### 5. Think and Work Holistically

**Definition**: No service or element stands alone. Understand and account for the interconnections.

**In Practice**:
- Consider all four dimensions of service management (see document 030)
- Understand dependencies and ripple effects
- Design end-to-end workflows, not isolated tasks
- Balance automation with human judgment

**Do**:
✅ Map service dependencies before making changes  
✅ Consider people, process, technology, and partners together  
✅ Think upstream (root causes) and downstream (customer impact)  
✅ Design value streams that span team boundaries

**Don't**:
❌ Optimize one part of the system at the expense of the whole  
❌ Ignore cultural impacts of technology changes  
❌ Forget that partner/vendor actions affect your services

**Example**: When optimizing incident response time, consider the ripple effects: Does faster resolution require more on-call staff? Will it reduce problem analysis quality? How does it affect customer trust?

### 6. Keep It Simple and Practical

**Definition**: Use the minimum number of steps to accomplish an objective. Always question whether complexity is necessary.

**In Practice**:
- Eliminate unnecessary activities and bureaucracy
- Design processes that people will actually follow
- Use "just enough" governance, not maximum control
- Favor pragmatism over perfection

**Do**:
✅ Ask "Can we remove this approval step without increasing risk?"  
✅ Use decision trees or automation for routine decisions  
✅ Eliminate fields in forms that nobody uses  
✅ Write procedures in plain language, not legalese

**Don't**:
❌ Create 20-step approval workflows for low-risk changes  
❌ Build complex processes that people bypass in emergencies  
❌ Require excessive documentation that nobody reads

**Example**: If 95% of service requests are approved without changes, don't require three levels of approval—automate approval for standard requests.

### 7. Optimize and Automate

**Definition**: Maximize the value of work carried out by human and technical resources.

**In Practice**:
- Automate repetitive, manual tasks
- Optimize before automating (don't automate waste)
- Use technology to augment human capabilities, not replace judgment
- Continuously seek efficiency improvements

**Do**:
✅ Automate password resets, account provisioning, routine approvals  
✅ Use monitoring to detect incidents before users report them  
✅ Implement self-service portals for common requests  
✅ Let humans focus on complex problems that require judgment

**Don't**:
❌ Automate a broken process—fix it first  
❌ Over-automate to the point where no human can intervene when needed  
❌ Ignore the human cost—automation that frustrates users is not optimization

**Example**: Automate standard server patching, but keep humans in the loop for critical production systems where context matters.

## How the Principles Work Together

The principles are interconnected and reinforce each other:

- **Focus on value** tells you **what** to optimize and automate
- **Start where you are** keeps you **simple and practical** by reusing assets
- **Iterate with feedback** enables **collaboration and visibility**
- **Think holistically** ensures **optimization** doesn't sub-optimize
- **Simplicity** makes **collaboration** easier
- **Automation** allows **progress** at scale

## Applying Principles to Decision-Making

When facing a decision:

1. **Focus on value**: What outcome are we trying to achieve?
2. **Start where you are**: What do we already have that helps?
3. **Progress iteratively**: Can we test this in a small scope first?
4. **Collaborate**: Who else should weigh in?
5. **Think holistically**: What are the second-order effects?
6. **Keep it simple**: Is there a simpler approach?
7. **Optimize and automate**: Can we make this more efficient?

## Signals of Principle Adoption

1. Teams explicitly reference principles when debating options
2. "Does this create value?" is a common question in meetings
3. Pilots and MVPs are the norm, not "big bang" rollouts
4. Cross-functional collaboration happens naturally
5. Bureaucratic processes are regularly pruned
6. Automation initiatives start with process optimization
7. Decisions consider system-wide impacts

## Anti-Patterns: Principle Violations

🚫 **Value Ignored**: Implementing ITIL practices because "best practice says so" without asking if it creates value  
🚫 **Clean Slate Syndrome**: Rebuilding everything from scratch, ignoring existing assets  
🚫 **Waterfall Mindset**: 18-month implementation plans with no interim value delivery  
🚫 **Siloed Execution**: Teams working in isolation, surprised by each other's changes  
🚫 **Complexity Fetish**: 50-page process documents nobody follows  
🚫 **Premature Automation**: Automating broken workflows that should be eliminated

## Cross-Links

- **Service Value System**: See document 010_service_value_system.md
- **Four Dimensions**: See document 030_four_dimensions.md
- **Continual Improvement**: See document 160_continual_improvement.md

## Common Q&A

**Q: Are these principles mandatory?**  
A: They're guidance, not rules. However, ignoring them typically leads to poorer outcomes. Treat them as strong recommendations.

**Q: What if two principles conflict?**  
A: They rarely conflict in practice—usually one is being misapplied. Example: "Start where you are" doesn't mean "never improve"; it means "improve what exists rather than rebuild unnecessarily."

**Q: Which principle is most important?**  
A: "Focus on value" is foundational—it drives why you do anything. But all seven work together; cherry-picking one undermines the system.

**Q: How do principles apply to legacy systems?**  
A: "Start where you are" says assess them. "Focus on value" asks if they deliver value. "Iterate" suggests incremental modernization. "Optimize" may mean retire what's not valuable.

**Q: Can principles be used outside IT?**  
A: Yes. They're universal business principles. HR, Finance, Operations can apply them to any initiative.

## Summary

The seven guiding principles provide a philosophical foundation for ITIL v4 implementation. They ensure that process adoption doesn't become "process for process sake" and that all activities tie back to value creation. Organizations should use the principles to guide decisions at every level—strategic, tactical, and operational.
