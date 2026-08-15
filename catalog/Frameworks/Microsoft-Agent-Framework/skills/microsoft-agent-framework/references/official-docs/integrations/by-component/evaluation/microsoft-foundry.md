---
title: Microsoft Foundry evaluation
description: Evaluate Agent Framework agents, workflows, traces, and responses with Microsoft Foundry.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                  | C# | Python | Go | Notes                      |
  |--------------------------|:--:|:------:|:--:|----------------------------|
  | Quality evaluators       | ✅ |   ✅   | ❌ |                            |
  | Existing response eval   | ✅ |   ✅   | ❌ |                            |
  | Run and evaluate         | ✅ |   ✅   | ❌ |                            |
  | Rubric evaluators        | ✅ |   ✅   | ❌ |                            |
  | Trace evaluation         | ❌ |   ✅   | ❌ | Python sample available    |
  | Go availability          | ✅ |   ✅   | ✅ | Go zone is status only     |
-->

# Microsoft Foundry evaluation

`FoundryEvals` connects the Agent Framework evaluation APIs to Microsoft Foundry's managed evaluation service. It provides quality, safety, tool-use, agent-behavior, and rubric evaluators, with stored reports available in the Foundry portal.

For `EvalItem`, local checks, custom evaluators, and conversation split strategies, see [Agent evaluation](../../../agents/evaluation.md).

## Prerequisites

- A Microsoft Foundry project and model deployment.
- A project-scoped Foundry endpoint.
- Permission to submit evaluations and read reports.

:::zone pivot="programming-language-csharp"

## Evaluate responses or test queries

Configure `FoundryEvals`, then evaluate responses already generated or let `EvaluateAsync` run the agent for each query.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/05-end-to-end/Evaluation/Evaluation_FoundryQuality/Program.cs" range="12-47":::

The .NET samples also demonstrate Foundry rubric evaluators and per-dimension quality gates.

:::zone-end

:::zone pivot="programming-language-python"

## Evaluate an agent

Pass existing responses or test queries to `evaluate_agent()`. Results include pass/fail counts and the Foundry report URL.

:::code language="python" source="~/../agent-framework-code/python/samples/05-end-to-end/evaluation/foundry_evals/evaluate_agent_sample.py" range="44-95":::

Additional samples cover trace evaluation, tool-call evaluation, multi-turn evaluation, workflow evaluation, mixed providers, and custom Foundry rubrics.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Microsoft Foundry evaluation integration isn't currently available for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Quality gates

Pin datasets, model deployments, evaluator versions, and rubric versions when results must be comparable across runs. Use result assertion helpers to fail CI when required metrics regress.

## Next steps

> [!div class="nextstepaction"]
> [Agent evaluation](../../../agents/evaluation.md)
