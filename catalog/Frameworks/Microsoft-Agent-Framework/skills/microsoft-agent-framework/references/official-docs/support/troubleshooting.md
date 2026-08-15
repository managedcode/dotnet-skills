---
title: Troubleshooting
description: Common issues and solutions when working with Agent Framework.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 05/27/2026
ms.service: agent-framework
---

# Troubleshooting

This page covers common issues and solutions when working with Agent Framework.

> [!NOTE]
> This page is being restructured. Common troubleshooting scenarios will be added.

## Common Issues

### Authentication Errors

Ensure you have the correct credentials configured for your AI provider. For Azure OpenAI, verify:
- Azure CLI is installed and authenticated (`az login`)
- User has the `Cognitive Services OpenAI User` or `Cognitive Services OpenAI Contributor` role

### Package Installation Issues

:::zone pivot="programming-language-csharp"
Ensure you're using .NET 8.0 SDK or later. Run `dotnet --version` to check your installed version.
:::zone-end

:::zone pivot="programming-language-python"
Ensure you're using Python 3.10 or later. Run `python --version` to check your installed version.
:::zone-end

:::zone pivot="programming-language-go"

Ensure you're using Go 1.25 or later. Run `go version` to check your installed version. If dependencies fail to resolve, run `go mod tidy` and verify your module imports `github.com/microsoft/agent-framework-go` packages that exist in the current SDK.

:::zone-end
## Getting Help

If you can't find a solution here, visit our [GitHub Discussions](https://github.com/microsoft/agent-framework/discussions) for community support.

## Next steps

> [!div class="nextstepaction"]
> [FAQ](./faq.md)
