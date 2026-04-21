---
title: Model Context Protocol
description: Using MCP with Agent Framework
zone_pivot_groups: programming-languages
author: markwallace
ms.topic: reference
ms.author: markwallace
ms.date: 09/24/2025
ms.service: agent-framework
---

# Model Context Protocol

Model Context Protocol is an open standard that defines how applications provide tools and contextual data to large language models (LLMs). It enables consistent, scalable integration of external tools into model workflows.

You can extend the capabilities of your Agent Framework agents by connecting it to tools hosted on remote [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) servers.

## Considerations for using third party Model Context Protocol servers

Your use of Model Context Protocol servers is subject to the terms between you and the service provider. When you connect to a non-Microsoft service, some of your data (such as prompt content) is passed to the non-Microsoft service, or your application might receive data from the non-Microsoft service. You're responsible for your use of non-Microsoft services and data, along with any charges associated with that use.

The remote MCP servers that you decide to use with the MCP tool described in this article were created by third parties, not Microsoft. Microsoft hasn't tested or verified these servers. Microsoft has no responsibility to you or others in relation to your use of any remote MCP servers.

We recommend that you carefully review and track what MCP servers you add to your Agent Framework based applications. We also recommend that you rely on servers hosted by trusted service providers themselves rather than proxies.

The MCP tool allows you to pass custom headers, such as authentication keys or schemas, that a remote MCP server might need. We recommend that you review all data that's shared with remote MCP servers and that you log the data for auditing purposes. Be cognizant of non-Microsoft practices for retention and location of data.

## How it works

You can integrate multiple remote MCP servers by adding them as tools to your agent. Agent Framework makes it easy to convert an MCP tool to an AI tool that can be called by your agent.

The MCP tool supports custom headers, so you can connect to MCP servers by using the authentication schemas that they require or by passing other headers that the MCP servers require. **You can specify headers only by including them in tool_resources at each run. In this way, you can put API keys, OAuth access tokens, or other credentials directly in your request.**

The most commonly used header is the authorization header. Headers that you pass in are available only for the current run and aren't persisted.

For more information on using MCP, see:

- [Security Best Practices](https://modelcontextprotocol.io/specification/draft/basic/security_best_practices) on the Model Context Protocol website.
- [Understanding and mitigating security risks in MCP implementations](https://techcommunity.microsoft.com/blog/microsoft-security-blog/understanding-and-mitigating-security-risks-in-mcp-implementations/4404667) in the Microsoft Security Community Blog.

## Next steps

> [!div class="nextstepaction"]
> [Using MCP tools with Agents](./using-mcp-tools.md)
> [Using MCP tools with Foundry Agents](./using-mcp-with-foundry-agents.md)
