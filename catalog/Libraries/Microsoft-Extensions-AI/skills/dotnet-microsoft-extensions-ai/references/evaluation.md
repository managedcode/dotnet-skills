# Microsoft.Extensions.AI Evaluation

## Package Set

| Package | Purpose |
|---|---|
| `Microsoft.Extensions.AI.Evaluation` | Core evaluation abstractions and result types |
| `Microsoft.Extensions.AI.Evaluation.Quality` | LLM-based quality evaluators such as relevance, completeness, groundedness, and fluency |
| `Microsoft.Extensions.AI.Evaluation.NLP` | Non-LLM text-similarity evaluators such as BLEU, GLEU, and F1 |
| `Microsoft.Extensions.AI.Evaluation.Safety` | Safety evaluators backed by the Microsoft Foundry Evaluation service |
| `Microsoft.Extensions.AI.Evaluation.Reporting` | Result storage, cached responses, and report generation |
| `Microsoft.Extensions.AI.Evaluation.Reporting.Azure` | Azure Storage-backed reporting and caching support |
| `Microsoft.Extensions.AI.Evaluation.Console` | `dotnet aieval` CLI for reports and cache management |

## Choose Evaluators By Risk

### Quality

Use these when answer quality or agent behavior matters:

- `RelevanceEvaluator`
- `CompletenessEvaluator`
- `RetrievalEvaluator`
- `FluencyEvaluator`
- `CoherenceEvaluator`
- `EquivalenceEvaluator`
- `GroundednessEvaluator`
- `IntentResolutionEvaluator`
- `TaskAdherenceEvaluator`
- `ToolCallAccuracyEvaluator`

### NLP

Use these when you already have reference answers and need cheaper deterministic comparisons:

- `BLEUEvaluator`
- `GLEUEvaluator`
- `F1Evaluator`

### Safety

Use these when harmful output, prompt attacks, or unsafe code are part of the release risk:

- `ContentHarmEvaluator`
- `ProtectedMaterialEvaluator`
- `GroundednessProEvaluator`
- `UngroundedAttributesEvaluator`
- `HateAndUnfairnessEvaluator`
- `SelfHarmEvaluator`
- `ViolenceEvaluator`
- `SexualEvaluator`
- `CodeVulnerabilityEvaluator`
- `IndirectAttackEvaluator`

## Practical Evaluation Loop

1. Pick a stable prompt or scenario set that represents the real feature.
2. Decide whether the gate is about answer quality, tool behavior, safety, or all three.
3. Use the same `IChatClient`-backed app surface that production uses, or a controlled test double when you are isolating logic.
4. Cache responses for repeatability and lower cost.
5. Store results and publish reports so model, prompt, or middleware changes are comparable across runs.

## CI Guidance

- Use NLP evaluators for low-cost baseline checks on every PR when reference outputs exist.
- Use quality evaluators on targeted, high-value scenarios such as retrieval, summarization, tool use, or task adherence.
- Use safety evaluators for user-facing or code-producing features before release.
- Track threshold changes deliberately; do not quietly relax gates when a prompt or model regresses.

## Agent-Oriented Checks

Even if the app is not using full Agent Framework, agent-like workflows often need:

- `IntentResolutionEvaluator` when the system has to understand and complete multi-step user requests
- `TaskAdherenceEvaluator` when the system receives bounded instructions or policies
- `ToolCallAccuracyEvaluator` when local functions or MCP-backed tools are part of the flow

These metrics are often the first place where prompt drift or tool-schema changes show up.

## Reporting And Caching

- The libraries support response caching so unchanged prompt-model combinations can reuse prior results.
- Reporting packages let you persist evaluation data and generate human-readable reports.
- The `dotnet aieval` CLI is useful for report generation and cache management in local runs or CI pipelines.

## Common Failure Modes

- Evaluating only one happy-path prompt instead of the real scenario envelope.
- Comparing outputs without fixing the prompt, grounding data, or model selection.
- Treating evaluation as a one-time benchmark instead of a regression suite.
- Shipping tool-using or RAG features without measuring task adherence, groundedness, or tool accuracy.
