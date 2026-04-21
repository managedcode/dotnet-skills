# Troubleshooting

This page covers common issues and solutions when working with Agent Framework.

> Note
>
> This page is being restructured. Common troubleshooting scenarios will be added.

## Common Issues

### Authentication Errors

Ensure you have the correct credentials configured for your AI provider. For Azure OpenAI, verify:

- Azure CLI is installed and authenticated (`az login`)
- User has the `Cognitive Services OpenAI User` or `Cognitive Services OpenAI Contributor` role

### Package Installation Issues

Ensure you're using .NET 8.0 SDK or later. Run `dotnet --version` to check your installed version.

Ensure you're using Python 3.10 or later. Run `python --version` to check your installed version.

## Getting Help

If you can't find a solution here, visit our [GitHub Discussions](https://github.com/microsoft/agent-framework/discussions) for community support.
