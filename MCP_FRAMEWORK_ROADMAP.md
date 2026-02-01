# Monte Carlo Planning (MCP) Framework Implementation Roadmap

## Overview

This roadmap provides a comprehensive, task-oriented plan for implementing an agent-based Monte Carlo Planning (MCP) framework into the SmallMind repository. The implementation focuses on **performance**, **low memory footprint**, **low CPU consumption**, and adheres to SmallMind's core principle of **zero third-party dependencies** (pure C# implementation).

Engineers can reference specific tasks using the format **"task XYZ"** where XYZ is the task number (e.g., "task 1.1", "task 3.2").

## Architecture Overview

The MCP framework will integrate seamlessly with SmallMind's existing architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                    MCP Framework (New)                          │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Tree Search  │  │ State Space  │  │ Rollout      │          │
│  │ (MCTS/UCT)   │  │ Simulation   │  │ Evaluator    │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Action Space │  │ Memory Pool  │  │ Planning     │          │
│  │ Generator    │  │ Manager      │  │ Controller   │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
         │                  │                    │
         ▼                  ▼                    ▼
┌─────────────────────────────────────────────────────────────────┐
│              Existing SmallMind Infrastructure                  │
├─────────────────────────────────────────────────────────────────┤
│  Workflows │ Domain Reasoning │ InferenceEngine │ Transformers  │
│  Tokenizers│ State Management │ SIMD Kernels    │ Core Tensors  │
└─────────────────────────────────────────────────────────────────┘
```

## Design Principles

1. **Performance First**: SIMD-optimized operations, cache-friendly data structures
2. **Memory Efficiency**: Object pooling, pre-allocated buffers, minimal allocations
3. **Pure C#**: No external dependencies (System.* only)
4. **Incremental Build**: Each task is independently testable
5. **Production Ready**: Async/await, cancellation tokens, telemetry hooks
6. **Integration**: Leverages existing SmallMind components (Workflows, Domain Reasoning, InferenceEngine)

---

## Phase 1: Core Tree Search Infrastructure

### Task 1.1: Define Core Data Structures

**File**: `src/SmallMind/Planning/TreeNode.cs`

Create the foundational tree node structure for MCTS with memory-efficient layout.

**Requirements**:
- Immutable state reference (avoid state duplication)
- Visit count, total value, and UCB statistics
- Parent/children relationships using pooled arrays
- Span-based APIs for zero-copy child iteration
- Support for node expansion and backpropagation

**Performance Considerations**:
- Use `struct` for statistics to reduce heap allocations
- Store children as `ArraySegment<TreeNode>` to minimize GC pressure
- Cache computed UCB values with dirty flag
- Align data for cache line efficiency (64 bytes)

**Deliverables**:
```csharp
public sealed class TreeNode
{
    public readonly int StateHash;
    public readonly TreeNode? Parent;
    public int VisitCount { get; set; }
    public double TotalValue { get; set; }
    public ReadOnlySpan<TreeNode> Children { get; }
    public bool IsFullyExpanded { get; }
    public double UCBScore(double explorationConstant);
    public void AddChild(TreeNode child);
    public void Backpropagate(double value);
}
```

**Tests**:
- Node creation and parent linkage
- UCB calculation correctness
- Backpropagation accuracy
- Memory footprint validation (<128 bytes per node)

---

### Task 1.2: Implement Memory Pool for Tree Nodes

**File**: `src/SmallMind/Planning/TreeNodePool.cs`

Create a high-performance object pool to reduce GC pressure during tree growth.

**Requirements**:
- Pre-allocate node capacity based on budget
- Fast rent/return operations (O(1) amortized)
- Automatic expansion with configurable limits
- Thread-safe for parallel rollouts (ConcurrentBag or partitioned pools)
- Clear/reset functionality for node reuse

**Performance Considerations**:
- Use `ConcurrentBag<TreeNode>` for lock-free pooling
- Partition pools by thread ID to reduce contention
- Pre-warm pool on initialization
- Implement size buckets for different child counts

**Deliverables**:
```csharp
public sealed class TreeNodePool
{
    public TreeNodePool(int initialCapacity, int maxCapacity);
    public TreeNode Rent(int childCapacityHint = 4);
    public void Return(TreeNode node);
    public void Clear();
    public PoolStatistics GetStatistics();
}

public readonly struct PoolStatistics
{
    public int TotalAllocated { get; }
    public int CurrentInUse { get; }
    public int PoolHits { get; }
    public int PoolMisses { get; }
}
```

**Tests**:
- Rent/return cycle correctness
- Thread-safety under parallel access
- Memory leak detection (no unbounded growth)
- Performance: 1M rent/return ops < 50ms

---

### Task 1.3: Implement Action Space Interface

**File**: `src/SmallMind/Planning/IActionSpace.cs`

Define abstraction for generating valid actions at each state.

**Requirements**:
- Interface for domain-specific action enumeration
- Support for continuous and discrete action spaces
- Lazy action generation (IAsyncEnumerable)
- Action validation and legality checking
- Budget-aware action sampling

**Performance Considerations**:
- Use `IAsyncEnumerable<Action>` for streaming large action sets
- Avoid materialization when possible (lazy evaluation)
- Support action caching for repeated states

**Deliverables**:
```csharp
public interface IActionSpace<TState, TAction>
{
    IAsyncEnumerable<TAction> GenerateActionsAsync(
        TState state,
        CancellationToken cancellationToken = default);
    
    bool IsLegalAction(TState state, TAction action);
    int GetActionSpaceSize(TState state);
}

public sealed class TextGenerationActionSpace : IActionSpace<GenerationState, TokenAction>
{
    // Token-level action space for LLM planning
}

public sealed class StructuredActionSpace : IActionSpace<WorkflowState, WorkflowAction>
{
    // High-level actions (API calls, tool usage, etc.)
}
```

**Tests**:
- Action generation for sample states
- Legality validation
- Async enumeration cancellation
- Memory usage for large action spaces

---

### Task 1.4: Implement State Representation

**File**: `src/SmallMind/Planning/PlanningState.cs`

Create efficient state representation for planning.

**Requirements**:
- Immutable state snapshots
- Fast equality comparison (GetHashCode optimization)
- Serialization for state caching
- Integration with WorkflowState (reuse existing infrastructure)
- Copy-on-write semantics to minimize memory

**Performance Considerations**:
- Use ReadOnlyMemory<T> for large state components
- Implement custom hash function (xxHash or FNV-1a)
- Cache hash codes to avoid recomputation
- Use structural equality for state deduplication

**Deliverables**:
```csharp
public sealed class PlanningState : IEquatable<PlanningState>
{
    public readonly int Hash;
    public ReadOnlyMemory<float> Observations { get; }
    public WorkflowState? WorkflowContext { get; }
    public bool IsTerminal { get; }
    
    public bool Equals(PlanningState? other);
    public override int GetHashCode() => Hash;
    public PlanningState ApplyAction(object action);
}
```

**Tests**:
- Equality semantics
- Hash distribution quality
- State transition correctness
- Memory sharing validation

---

### Task 1.5: Implement UCB (Upper Confidence Bound) Calculator

**File**: `src/SmallMind/Planning/UCBCalculator.cs`

Implement UCB1 and UCT (UCB for Trees) selection strategies.

**Requirements**:
- UCB1 formula: `value/visits + C * sqrt(ln(parentVisits) / visits)`
- Configurable exploration constant C
- Support for UCB1-Tuned variant
- SIMD-optimized batch UCB calculation
- Handle edge cases (zero visits, terminal nodes)

**Performance Considerations**:
- Pre-compute log table for common visit counts
- Use SIMD to calculate UCB for multiple children simultaneously
- Cache sqrt(ln(N)) values per tree level

**Deliverables**:
```csharp
public sealed class UCBCalculator
{
    public UCBCalculator(double explorationConstant = 1.41);
    
    public double CalculateUCB(
        double nodeValue,
        int nodeVisits,
        int parentVisits);
    
    public void CalculateBatchUCB(
        ReadOnlySpan<TreeNode> nodes,
        int parentVisits,
        Span<double> ucbScores);
}
```

**Tests**:
- Correctness against reference implementation
- Edge case handling (zero visits)
- SIMD vs scalar parity
- Performance: 10K calculations < 1ms

---

## Phase 2: Monte Carlo Tree Search (MCTS) Engine

### Task 2.1: Implement MCTS Core Algorithm

**File**: `src/SmallMind/Planning/MCTSEngine.cs`

Build the main MCTS loop with selection, expansion, simulation, and backpropagation.

**Requirements**:
- Iterative deepening with time/iteration budgets
- Four MCTS phases: Selection → Expansion → Simulation → Backpropagation
- Parallel rollouts with thread-safe tree updates
- Progressive widening for large action spaces
- Async API with cancellation support

**Performance Considerations**:
- Use virtual loss for parallel MCTS (lock-free selection)
- Batch GPU-style parallel rollouts
- Minimize tree traversal overhead (cache parent pointers)
- Reuse simulation buffers across iterations

**Deliverables**:
```csharp
public sealed class MCTSEngine
{
    public MCTSEngine(MCTSOptions options);
    
    public async Task<MCTSResult> SearchAsync(
        PlanningState initialState,
        IActionSpace actionSpace,
        IStateEvaluator evaluator,
        CancellationToken cancellationToken = default);
    
    public IAsyncEnumerable<MCTSProgress> SearchStreamingAsync(...);
}

public sealed class MCTSOptions
{
    public int MaxIterations { get; set; } = 1000;
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxTreeDepth { get; set; } = 100;
    public double ExplorationConstant { get; set; } = 1.41;
    public int ParallelRollouts { get; set; } = 4;
    public int MaxTreeNodes { get; set; } = 100_000;
}

public sealed class MCTSResult
{
    public object BestAction { get; }
    public double BestValue { get; }
    public int TotalIterations { get; }
    public TimeSpan ElapsedTime { get; }
    public TreeNode RootNode { get; }
    public MCTSStatistics Statistics { get; }
}
```

**Tests**:
- Convergence on toy problems (Tic-Tac-Toe, simple game trees)
- Budget enforcement (time and iteration limits)
- Parallel safety (no race conditions)
- Best action selection correctness

---

### Task 2.2: Implement Selection Phase

**File**: `src/SmallMind/Planning/SelectionStrategy.cs`

Implement tree policy for selecting nodes to expand.

**Requirements**:
- UCB-based selection (highest UCB score)
- Support for alternative strategies (Thompson Sampling, RAVE)
- Early stopping on terminal nodes
- Path recording for backpropagation

**Performance Considerations**:
- Inline UCB calculation during traversal
- Avoid List allocations for path (use ArrayPool)
- Optimize for common case (small branching factor)

**Deliverables**:
```csharp
public interface ISelectionStrategy
{
    TreeNode Select(TreeNode root, Span<TreeNode> pathBuffer);
}

public sealed class UCBSelectionStrategy : ISelectionStrategy
{
    public TreeNode Select(TreeNode root, Span<TreeNode> pathBuffer);
}
```

**Tests**:
- Selection behavior on balanced trees
- Preference for unexplored nodes
- Path correctness

---

### Task 2.3: Implement Expansion Phase

**File**: `src/SmallMind/Planning/ExpansionStrategy.cs`

Expand tree by adding child nodes for untried actions.

**Requirements**:
- Generate legal actions from action space
- Create child nodes using node pool
- Progressive widening (limit children per node)
- Avoid duplicate state expansion (transposition table)

**Performance Considerations**:
- Lazy child creation (expand on first selection)
- Batch allocate children from pool
- Use hash table for transposition detection

**Deliverables**:
```csharp
public interface IExpansionStrategy
{
    Task<TreeNode?> ExpandAsync(
        TreeNode node,
        PlanningState state,
        IActionSpace actionSpace,
        TreeNodePool nodePool,
        CancellationToken cancellationToken = default);
}

public sealed class ProgressiveWideningExpansion : IExpansionStrategy
{
    public ProgressiveWideningExpansion(double wideningConstant = 0.5);
    // ...
}
```

**Tests**:
- Child creation correctness
- Progressive widening limits
- Transposition handling

---

### Task 2.4: Implement Simulation (Rollout) Phase

**File**: `src/SmallMind/Planning/RolloutStrategy.cs`

Simulate game/task from current state to terminal state.

**Requirements**:
- Default policy (random or heuristic-guided)
- Integration with InferenceEngine for LLM-based rollouts
- Depth-limited simulation with early termination
- Reusable simulation buffers

**Performance Considerations**:
- Use stack-allocated arrays for short rollouts
- Batch inference calls for LLM rollouts
- Cache rollout results for identical states

**Deliverables**:
```csharp
public interface IRolloutStrategy
{
    Task<double> SimulateAsync(
        PlanningState state,
        IActionSpace actionSpace,
        int maxDepth,
        CancellationToken cancellationToken = default);
}

public sealed class RandomRolloutStrategy : IRolloutStrategy { }

public sealed class LLMGuidedRolloutStrategy : IRolloutStrategy
{
    public LLMGuidedRolloutStrategy(IInferenceEngine engine);
    // ...
}
```

**Tests**:
- Rollout completion to terminal states
- Depth limiting
- Value range correctness [0, 1]

---

### Task 2.5: Implement Backpropagation Phase

**File**: `src/SmallMind/Planning/BackpropagationStrategy.cs`

Propagate simulation results back up the tree.

**Requirements**:
- Update visit counts and values
- Support for different value aggregation (mean, max, UCB-based)
- Thread-safe updates for parallel MCTS
- RAVE (Rapid Action Value Estimation) support

**Performance Considerations**:
- Use Interlocked operations for atomic updates
- Avoid locking (lock-free backpropagation)
- Batch update multiple nodes in path

**Deliverables**:
```csharp
public interface IBackpropagationStrategy
{
    void Backpropagate(ReadOnlySpan<TreeNode> path, double value);
}

public sealed class MeanBackpropagation : IBackpropagationStrategy { }
public sealed class VirtualLossBackpropagation : IBackpropagationStrategy { }
```

**Tests**:
- Value propagation correctness
- Thread-safety under parallel backprop
- Visit count accuracy

---

## Phase 3: State Evaluation and Value Functions

### Task 3.1: Implement State Evaluator Interface

**File**: `src/SmallMind/Planning/IStateEvaluator.cs`

Define abstraction for evaluating non-terminal states.

**Requirements**:
- Async evaluation API
- Batch evaluation support
- Confidence/uncertainty estimation
- Integration with InferenceEngine

**Performance Considerations**:
- Batch inference for multiple states
- Cache evaluations for repeated states
- Use half-precision for value estimates (float16)

**Deliverables**:
```csharp
public interface IStateEvaluator
{
    Task<StateValue> EvaluateAsync(
        PlanningState state,
        CancellationToken cancellationToken = default);
    
    Task<StateValue[]> EvaluateBatchAsync(
        ReadOnlyMemory<PlanningState> states,
        CancellationToken cancellationToken = default);
}

public readonly struct StateValue
{
    public double Value { get; }
    public double Confidence { get; }
    public bool IsTerminal { get; }
}
```

**Tests**:
- Single and batch evaluation
- Value range validation
- Terminal state detection

---

### Task 3.2: Implement Heuristic Evaluator

**File**: `src/SmallMind/Planning/HeuristicEvaluator.cs`

Fast rule-based state evaluation (no inference).

**Requirements**:
- Domain-specific heuristics (configurable)
- Fast computation (<1ms per state)
- Admissible heuristics for optimal planning
- Caching for expensive heuristics

**Performance Considerations**:
- SIMD-optimized feature extraction
- Lookup tables for common patterns
- Inline simple heuristics

**Deliverables**:
```csharp
public sealed class HeuristicEvaluator : IStateEvaluator
{
    public HeuristicEvaluator(Func<PlanningState, double> heuristicFunc);
    public Task<StateValue> EvaluateAsync(...);
}
```

**Tests**:
- Heuristic correctness on known states
- Performance benchmarks
- Value consistency

---

### Task 3.3: Implement LLM-Based Evaluator

**File**: `src/SmallMind/Planning/LLMStateEvaluator.cs`

Use InferenceEngine for learned state evaluation.

**Requirements**:
- Prompt engineering for value estimation
- Parsing LLM output to numerical values
- Uncertainty quantification (logit variance)
- Batch processing for efficiency

**Performance Considerations**:
- Reuse KV-cache across evaluations
- Batch multiple states in single prompt
- Use quantized models for speed

**Deliverables**:
```csharp
public sealed class LLMStateEvaluator : IStateEvaluator
{
    public LLMStateEvaluator(
        IInferenceEngine engine,
        ITokenizer tokenizer,
        string evaluationPromptTemplate);
    
    public Task<StateValue> EvaluateAsync(...);
    public Task<StateValue[]> EvaluateBatchAsync(...);
}
```

**Tests**:
- Value extraction from LLM output
- Batch processing correctness
- Timeout handling

---

### Task 3.4: Implement Composite Evaluator

**File**: `src/SmallMind/Planning/CompositeEvaluator.cs`

Combine multiple evaluators with weighted voting.

**Requirements**:
- Weighted ensemble of evaluators
- Fallback on evaluator failure
- Parallel evaluation with timeout
- Confidence aggregation

**Performance Considerations**:
- Run evaluators in parallel (Task.WhenAll)
- Short-circuit on high-confidence results
- Cache ensemble results

**Deliverables**:
```csharp
public sealed class CompositeEvaluator : IStateEvaluator
{
    public CompositeEvaluator(params (IStateEvaluator, double)[] evaluators);
    public Task<StateValue> EvaluateAsync(...);
}
```

**Tests**:
- Weighted combination correctness
- Fallback behavior
- Parallel execution

---

## Phase 4: Planning Controller and Integration

### Task 4.1: Implement Planning Controller

**File**: `src/SmallMind/Planning/PlanningController.cs`

High-level controller orchestrating planning and execution.

**Requirements**:
- Plan-then-execute loop
- Replanning on environment changes
- Budget management (tokens, time, compute)
- Integration with WorkflowRunner
- Streaming progress events

**Performance Considerations**:
- Amortize planning cost across multiple steps
- Reuse partial trees on replanning
- Async/await throughout

**Deliverables**:
```csharp
public sealed class PlanningController
{
    public PlanningController(PlanningControllerOptions options);
    
    public async Task<PlanningResult> PlanAndExecuteAsync(
        PlanningState initialState,
        PlanningGoal goal,
        CancellationToken cancellationToken = default);
    
    public IAsyncEnumerable<PlanningEvent> PlanAndExecuteStreamingAsync(...);
}

public sealed class PlanningControllerOptions
{
    public MCTSOptions MCTSOptions { get; set; }
    public int ReplanningInterval { get; set; } = 5;
    public TimeSpan TotalBudget { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxPlanLength { get; set; } = 50;
}

public sealed class PlanningResult
{
    public IReadOnlyList<object> Actions { get; }
    public double TotalValue { get; }
    public PlanningState FinalState { get; }
    public PlanningStatistics Statistics { get; }
}
```

**Tests**:
- Plan generation for simple goals
- Replanning trigger
- Budget enforcement

---

### Task 4.2: Implement Planning Goal Specification

**File**: `src/SmallMind/Planning/PlanningGoal.cs`

Define goal conditions for planning termination.

**Requirements**:
- Predicate-based goal checking
- Soft goals (optimization objectives)
- Hard constraints (must satisfy)
- Partial goal achievement tracking

**Performance Considerations**:
- Fast goal checking (inline predicates)
- Cache goal evaluations

**Deliverables**:
```csharp
public sealed class PlanningGoal
{
    public Func<PlanningState, bool> IsAchieved { get; set; }
    public Func<PlanningState, double> Reward { get; set; }
    public IReadOnlyList<IConstraint> Constraints { get; set; }
}

public interface IConstraint
{
    bool IsSatisfied(PlanningState state);
    double ViolationPenalty(PlanningState state);
}
```

**Tests**:
- Goal achievement detection
- Constraint validation
- Reward computation

---

### Task 4.3: Implement Integration with Workflows

**File**: `src/SmallMind/Planning/WorkflowPlanningAdapter.cs`

Adapt existing WorkflowRunner to use planning.

**Requirements**:
- Convert WorkflowStep to planning actions
- Map WorkflowState to PlanningState
- Automatic action execution via InferenceEngine
- Fallback to non-planning execution

**Performance Considerations**:
- Minimize state conversions
- Reuse workflow infrastructure (no duplication)

**Deliverables**:
```csharp
public sealed class WorkflowPlanningAdapter
{
    public WorkflowPlanningAdapter(IWorkflowRunner workflowRunner);
    
    public IActionSpace<PlanningState, WorkflowAction> CreateActionSpace(
        WorkflowDefinition workflow);
    
    public async Task<WorkflowRunResult> ExecuteWithPlanningAsync(
        WorkflowDefinition workflow,
        WorkflowRunRequest request,
        PlanningControllerOptions planningOptions,
        CancellationToken cancellationToken = default);
}
```

**Tests**:
- Workflow-to-planning conversion
- Action execution correctness
- Fallback behavior

---

### Task 4.4: Implement Integration with Domain Reasoning

**File**: `src/SmallMind/Planning/DomainPlanningAdapter.cs`

Use DomainReasoner for policy-constrained planning.

**Requirements**:
- Enforce DomainProfile policies during planning
- Use DomainReasoner for action generation
- Automatic safety filtering of plans
- Out-of-domain detection

**Performance Considerations**:
- Batch domain queries
- Cache policy checks

**Deliverables**:
```csharp
public sealed class DomainPlanningAdapter
{
    public DomainPlanningAdapter(IDomainReasoner reasoner);
    
    public IActionSpace<PlanningState, DomainAction> CreateActionSpace(
        DomainProfile domain);
    
    public async Task<DomainAnswer> PlanAndAnswerAsync(
        DomainQuestion question,
        PlanningControllerOptions planningOptions,
        CancellationToken cancellationToken = default);
}
```

**Tests**:
- Policy enforcement during planning
- Safety violation detection
- Domain-constrained action generation

---

## Phase 5: Performance Optimization

### Task 5.1: Implement SIMD-Optimized UCB Calculation

**File**: `src/SmallMind/Planning/Simd/SimdUCBCalculator.cs`

Vectorized UCB computation for batch node evaluation.

**Requirements**:
- AVX2/SSE2 implementations
- ARM NEON support
- Fallback to scalar for unsupported CPUs
- Batch processing of 4/8/16 nodes simultaneously

**Performance Considerations**:
- Align data for SIMD loads (16/32-byte boundaries)
- Use gather instructions for sparse data
- Minimize horizontal operations

**Deliverables**:
```csharp
public static class SimdUCBCalculator
{
    public static void CalculateBatchUCB(
        ReadOnlySpan<double> nodeValues,
        ReadOnlySpan<int> nodeVisits,
        int parentVisits,
        double explorationConstant,
        Span<double> ucbScores);
}
```

**Tests**:
- SIMD vs scalar parity
- Performance benchmarks (>4x speedup)
- Edge case handling

---

### Task 5.2: Implement Memory-Efficient State Caching

**File**: `src/SmallMind/Planning/StateCache.cs`

Cache state evaluations and action expansions.

**Requirements**:
- LRU eviction policy
- Configurable memory budget
- Thread-safe concurrent access
- Hash-based lookups (O(1) average)

**Performance Considerations**:
- Use ConcurrentDictionary with custom comparer
- Store only hash + value (not full state)
- Implement generational cache (short-term + long-term)

**Deliverables**:
```csharp
public sealed class StateCache
{
    public StateCache(int maxEntries, long maxMemoryBytes);
    
    public bool TryGetValue(int stateHash, out StateValue value);
    public void SetValue(int stateHash, StateValue value);
    public void Clear();
    public CacheStatistics GetStatistics();
}
```

**Tests**:
- Hit/miss correctness
- LRU eviction
- Memory limit enforcement
- Thread-safety

---

### Task 5.3: Implement Parallel MCTS with Virtual Loss

**File**: `src/SmallMind/Planning/ParallelMCTS.cs`

Enable concurrent rollouts without tree locking.

**Requirements**:
- Virtual loss on node selection
- Atomic backpropagation (Interlocked operations)
- Partitioned node pools per thread
- Load balancing across workers

**Performance Considerations**:
- Minimize contention (virtual loss reduces collisions)
- Use ThreadLocal for per-thread state
- Batch updates to reduce atomic ops

**Deliverables**:
```csharp
public sealed class ParallelMCTS
{
    public ParallelMCTS(int degreeOfParallelism);
    
    public async Task<MCTSResult> SearchAsync(...);
}
```

**Tests**:
- Correctness vs sequential MCTS
- Scalability (linear speedup up to 4-8 threads)
- No deadlocks or race conditions

---

### Task 5.4: Implement Incremental Tree Reuse

**File**: `src/SmallMind/Planning/TreeReuseStrategy.cs`

Reuse previous search trees for replanning.

**Requirements**:
- Identify reusable subtrees
- Prune invalidated branches
- Update root statistics
- Garbage collect unreachable nodes

**Performance Considerations**:
- Mark-and-sweep for subtree pruning
- Batch node returns to pool
- Lazy pruning (defer until memory pressure)

**Deliverables**:
```csharp
public sealed class TreeReuseStrategy
{
    public TreeNode? ReuseTree(
        TreeNode oldRoot,
        PlanningState newRootState,
        out int nodesReused,
        out int nodesPruned);
}
```

**Tests**:
- Subtree identification correctness
- Memory reclamation
- Performance (reuse faster than rebuild)

---

### Task 5.5: Implement Adaptive Budget Allocation

**File**: `src/SmallMind/Planning/AdaptiveBudgetAllocator.cs`

Dynamically allocate search budget based on state complexity.

**Requirements**:
- Difficulty estimation (branching factor, depth)
- Allocate more iterations to critical decisions
- Reserve budget for future steps
- Exponential smoothing for budget history

**Performance Considerations**:
- Fast complexity estimation (<1ms)
- Amortized budget tracking

**Deliverables**:
```csharp
public sealed class AdaptiveBudgetAllocator
{
    public int AllocateIterations(
        PlanningState state,
        int remainingTotalBudget,
        int stepsRemaining);
}
```

**Tests**:
- Budget allocation fairness
- Critical decision detection
- Total budget conservation

---

## Phase 6: Testing and Benchmarking

### Task 6.1: Create Unit Tests for Core Components

**Files**: `tests/SmallMind.Tests/Planning/*.cs`

Comprehensive unit tests for all planning components.

**Requirements**:
- TreeNode, TreeNodePool, UCBCalculator tests
- MCTS phase tests (Selection, Expansion, Simulation, Backpropagation)
- State evaluator tests
- Action space tests
- Goal specification tests

**Coverage Target**: >90% code coverage

**Deliverables**:
- `TreeNodeTests.cs`
- `MCTSEngineTests.cs`
- `StateEvaluatorTests.cs`
- `PlanningControllerTests.cs`

---

### Task 6.2: Create Integration Tests

**Files**: `tests/SmallMind.IntegrationTests/Planning/*.cs`

End-to-end planning scenarios.

**Requirements**:
- Simple game solving (Tic-Tac-Toe, simple mazes)
- Workflow planning with domain constraints
- Multi-agent scenarios
- Replanning on environment changes

**Deliverables**:
- `SimplePlanningTests.cs`
- `WorkflowPlanningTests.cs`
- `DomainConstrainedPlanningTests.cs`

---

### Task 6.3: Create Performance Benchmarks

**Files**: `benchmarks/Planning/*.cs`

BenchmarkDotNet-based performance tests.

**Requirements**:
- MCTS iteration throughput
- Memory allocation per iteration
- SIMD optimization validation
- Parallel MCTS scalability
- Cache hit rate measurement

**Deliverables**:
```csharp
[MemoryDiagnoser]
public class MCTSBenchmarks
{
    [Benchmark]
    public MCTSResult SearchTicTacToe() { }
    
    [Benchmark]
    public void UCBCalculationBatch() { }
    
    [Benchmark]
    public void ParallelRollouts() { }
}
```

---

### Task 6.4: Create Performance Regression Tests

**Files**: `tests/SmallMind.PerfTests/Planning/*.cs`

Detect performance degradation.

**Requirements**:
- Baseline metrics for all operations
- Automated regression detection (<10% tolerance)
- Memory leak detection
- CI integration

**Deliverables**:
- `PlanningPerformanceTests.cs`

---

## Phase 7: Documentation and Examples

### Task 7.1: Create API Documentation

**File**: `docs/planning/API_REFERENCE.md`

Complete API reference for planning framework.

**Requirements**:
- XML doc comments on all public APIs
- Code examples for each major class
- Parameter descriptions and constraints
- Performance characteristics documentation

---

### Task 7.2: Create Usage Guide

**File**: `docs/planning/USAGE_GUIDE.md`

Practical guide for using the planning framework.

**Requirements**:
- Quick start example
- Common patterns (workflow planning, domain-constrained planning)
- Performance tuning guide
- Troubleshooting section

---

### Task 7.3: Create Architecture Document

**File**: `docs/planning/ARCHITECTURE.md`

Deep dive into planning framework design.

**Requirements**:
- Component diagram
- Data flow diagrams
- Performance optimization rationale
- Design decision justifications

---

### Task 7.4: Create Example: Text Adventure Planning

**File**: `examples/Planning/TextAdventureAgent/`

Interactive text adventure with planning.

**Requirements**:
- Use MCTS to plan action sequences
- Natural language action space
- Goal: Solve text adventure game
- Performance metrics display

---

### Task 7.5: Create Example: API Orchestration Planning

**File**: `examples/Planning/APIOrchestration/`

Plan and execute multi-step API workflows.

**Requirements**:
- Plan sequence of API calls to achieve goal
- Constraint satisfaction (rate limits, dependencies)
- Error handling and replanning
- Integration with WorkflowRunner

---

## Phase 8: Production Hardening

### Task 8.1: Implement Logging and Telemetry

**File**: `src/SmallMind/Planning/PlanningTelemetry.cs`

Comprehensive logging and metrics.

**Requirements**:
- Structured logging (Microsoft.Extensions.Logging)
- Performance counters (iterations/sec, avg depth, cache hit rate)
- Error tracking
- Trace correlation IDs

**Deliverables**:
```csharp
public sealed class PlanningTelemetry
{
    public void RecordIteration(int depth, double bestValue);
    public void RecordCacheHit();
    public void RecordCacheMiss();
    public void RecordError(Exception ex);
    public TelemetrySnapshot GetSnapshot();
}
```

---

### Task 8.2: Implement Health Checks

**File**: `src/SmallMind/Planning/PlanningHealthCheck.cs`

ASP.NET Core health check integration.

**Requirements**:
- Check tree node pool capacity
- Monitor memory usage
- Validate evaluator responsiveness
- Report overall planning system health

**Deliverables**:
```csharp
public sealed class PlanningHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(...);
}
```

---

### Task 8.3: Implement Graceful Degradation

**File**: `src/SmallMind/Planning/DegradationStrategies.cs`

Fallback behaviors under resource constraints.

**Requirements**:
- Reduce search depth on timeout
- Fallback to greedy selection
- Disable caching on memory pressure
- Skip evaluations (use heuristics only)

**Deliverables**:
```csharp
public interface IDegradationStrategy
{
    MCTSOptions Degrade(MCTSOptions current, ResourceStatus status);
}
```

---

### Task 8.4: Implement Configuration Validation

**File**: `src/SmallMind/Planning/PlanningOptionsValidator.cs`

Validate planning configurations at startup.

**Requirements**:
- Parameter range checks
- Consistency validation (e.g., max depth < max iterations)
- Dependency validation (evaluator not null)
- Performance warnings (inefficient configurations)

**Deliverables**:
```csharp
public static class PlanningOptionsValidator
{
    public static ValidationResult Validate(PlanningControllerOptions options);
}
```

---

### Task 8.5: Implement Dependency Injection Extensions

**File**: `src/SmallMind/Planning/DependencyInjection/ServiceCollectionExtensions.cs`

Register planning services with DI container.

**Requirements**:
- Single method registration (AddPlanning())
- Configurable options
- Lifetime management (Singleton, Scoped)
- Health check registration

**Deliverables**:
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlanning(
        this IServiceCollection services,
        Action<PlanningControllerOptions>? configure = null);
}
```

---

## Task Dependency Graph

```
Phase 1: Core Infrastructure
├── 1.1: TreeNode ──┬──> 1.2: TreeNodePool ──┬──> 2.1: MCTSEngine
│                   │                          │
├── 1.3: IActionSpace ─────────────────────────┤
├── 1.4: PlanningState ────────────────────────┤
└── 1.5: UCBCalculator ────────────────────────┘

Phase 2: MCTS Algorithm
├── 2.1: MCTSEngine ──> 2.2: SelectionStrategy ──┐
│                  └──> 2.3: ExpansionStrategy ──┤
│                  └──> 2.4: RolloutStrategy ────┤──> 4.1: PlanningController
│                  └──> 2.5: BackpropagationStrategy ──┘

Phase 3: State Evaluation
├── 3.1: IStateEvaluator ──> 3.2: HeuristicEvaluator ──┐
│                       └──> 3.3: LLMStateEvaluator ────┤──> 4.1
│                       └──> 3.4: CompositeEvaluator ───┘

Phase 4: Integration
├── 4.1: PlanningController ──> 4.2: PlanningGoal
│                            └──> 4.3: WorkflowAdapter
│                            └──> 4.4: DomainAdapter

Phase 5: Optimization
├── 5.1: SimdUCBCalculator (enhances 1.5)
├── 5.2: StateCache (enhances 2.1)
├── 5.3: ParallelMCTS (enhances 2.1)
├── 5.4: TreeReuseStrategy (enhances 4.1)
└── 5.5: AdaptiveBudgetAllocator (enhances 4.1)

Phase 6: Testing
└── All phases ──> 6.1-6.4: Tests

Phase 7: Documentation
└── All phases ──> 7.1-7.5: Docs & Examples

Phase 8: Production
└── All phases ──> 8.1-8.5: Production Hardening
```

---

## Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| **MCTS Iteration Throughput** | >1000 iterations/sec | Simple state space, single thread |
| **Memory per TreeNode** | <128 bytes | Object size measurement |
| **UCB Calculation (batch)** | >100K calculations/sec | SIMD-optimized |
| **State Cache Hit Rate** | >70% | Typical planning scenario |
| **Parallel Speedup** | >3x on 4 cores | Parallel MCTS vs sequential |
| **Tree Reuse Efficiency** | >50% nodes reused | Replanning scenario |
| **Memory Footprint** | <100MB | 100K node tree |
| **Planning Latency (simple)** | <1 second | Tic-Tac-Toe optimal move |
| **Planning Latency (complex)** | <30 seconds | 50-step workflow planning |

---

## Integration Checklist

- [ ] Planning framework integrates with existing `InferenceEngine`
- [ ] Planning framework integrates with existing `WorkflowRunner`
- [ ] Planning framework integrates with existing `DomainReasoner`
- [ ] Planning framework uses existing `ITokenizer` and `Sampling`
- [ ] Planning framework uses SIMD infrastructure from `SmallMind.Core`
- [ ] Planning framework follows existing DI patterns (`ServiceCollectionExtensions`)
- [ ] Planning framework uses existing logging (`Microsoft.Extensions.Logging`)
- [ ] Planning framework follows existing async/await patterns
- [ ] Planning framework uses existing validation (`Guard` class)
- [ ] No third-party dependencies added (pure System.* only)

---

## Memory Budget Guidelines

| Component | Budget | Justification |
|-----------|--------|---------------|
| **TreeNode** | 128 bytes/node | Compact node representation |
| **TreeNodePool** | 10MB baseline | 100K pre-allocated nodes |
| **StateCache** | 50MB max | LRU cache for evaluations |
| **Simulation Buffers** | 5MB per thread | Reusable rollout buffers |
| **Total Planning System** | <100MB | Suitable for production deployment |

---

## CPU Budget Guidelines

| Operation | Budget | Justification |
|-----------|--------|---------------|
| **MCTS Iteration** | <1ms average | 1000 iterations/sec target |
| **UCB Calculation** | <10μs | Inline during selection |
| **State Evaluation (heuristic)** | <100μs | Fast rule-based |
| **State Evaluation (LLM)** | <50ms | Amortized via batching |
| **Tree Expansion** | <500μs | Action generation + node creation |
| **Rollout Simulation** | <10ms | Depth-limited to 20 steps |

---

## Risk Mitigation

| Risk | Mitigation Strategy |
|------|---------------------|
| **Memory Explosion** | - Tree node pool with hard limits<br>- LRU cache eviction<br>- Depth-limited search |
| **Slow Convergence** | - Adaptive budget allocation<br>- Progressive widening<br>- Heuristic initialization |
| **Thread Contention** | - Virtual loss for parallel MCTS<br>- Partitioned node pools<br>- Lock-free backpropagation |
| **Poor Action Quality** | - Ensemble evaluators<br>- Domain constraints<br>- Fallback to greedy |
| **Integration Complexity** | - Adapter pattern for existing components<br>- Incremental integration<br>- Backward compatibility |

---

## Success Criteria

1. **Functionality**: MCP framework solves simple planning problems (Tic-Tac-Toe, mazes) optimally
2. **Performance**: Meets all performance targets in benchmarks
3. **Memory**: Stays within budget guidelines (<100MB for large trees)
4. **Integration**: Seamlessly integrates with WorkflowRunner and DomainReasoner
5. **Code Quality**: >90% test coverage, zero critical bugs
6. **Documentation**: Complete API docs, usage guide, and examples
7. **Production Ready**: Logging, telemetry, health checks, graceful degradation

---

## Extensibility Points

Future enhancements (not in this roadmap):

- **AlphaZero-style Neural MCTS**: Replace rollouts with learned value/policy networks
- **Guided MCTS**: Use LLM to guide action selection (progressive widening)
- **Multi-Agent Planning**: Extend to adversarial/cooperative scenarios
- **Continuous Action Spaces**: Support for real-valued actions
- **Hierarchical Planning**: Task decomposition and abstract actions
- **Transfer Learning**: Reuse search trees across similar problems
- **Distributed MCTS**: Cluster-based parallel search

---

## Appendix: Task Reference Quick Index

**Phase 1: Core Infrastructure**
- Task 1.1: TreeNode
- Task 1.2: TreeNodePool
- Task 1.3: IActionSpace
- Task 1.4: PlanningState
- Task 1.5: UCBCalculator

**Phase 2: MCTS Algorithm**
- Task 2.1: MCTSEngine
- Task 2.2: SelectionStrategy
- Task 2.3: ExpansionStrategy
- Task 2.4: RolloutStrategy
- Task 2.5: BackpropagationStrategy

**Phase 3: State Evaluation**
- Task 3.1: IStateEvaluator
- Task 3.2: HeuristicEvaluator
- Task 3.3: LLMStateEvaluator
- Task 3.4: CompositeEvaluator

**Phase 4: Integration**
- Task 4.1: PlanningController
- Task 4.2: PlanningGoal
- Task 4.3: WorkflowPlanningAdapter
- Task 4.4: DomainPlanningAdapter

**Phase 5: Performance**
- Task 5.1: SimdUCBCalculator
- Task 5.2: StateCache
- Task 5.3: ParallelMCTS
- Task 5.4: TreeReuseStrategy
- Task 5.5: AdaptiveBudgetAllocator

**Phase 6: Testing**
- Task 6.1: Unit Tests
- Task 6.2: Integration Tests
- Task 6.3: Performance Benchmarks
- Task 6.4: Regression Tests

**Phase 7: Documentation**
- Task 7.1: API Documentation
- Task 7.2: Usage Guide
- Task 7.3: Architecture Document
- Task 7.4: Example: Text Adventure
- Task 7.5: Example: API Orchestration

**Phase 8: Production**
- Task 8.1: Logging and Telemetry
- Task 8.2: Health Checks
- Task 8.3: Graceful Degradation
- Task 8.4: Configuration Validation
- Task 8.5: Dependency Injection

---

## How to Request Tasks

Engineers can request specific implementation tasks using the following format:

**"Implement task X.Y"** or **"task X.Y"**

Examples:
- "Implement task 1.1" → Implement TreeNode data structure
- "task 2.1" → Implement MCTS core algorithm
- "Implement task 5.3" → Implement parallel MCTS with virtual loss

Each task is self-contained with:
- Clear deliverables
- Performance requirements
- Test criteria
- Integration points

Tasks can generally be implemented in dependency order (see Task Dependency Graph), but many tasks within a phase can be parallelized across team members.

---

**Document Version**: 1.0  
**Last Updated**: 2026-02-01  
**Maintained By**: SmallMind Core Team
