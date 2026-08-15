---
title: LLM Fundamentals
description: Understand how large language models work, their capabilities, limitations, and why they form the foundation of AI agents.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 04/02/2026
ms.service: agent-framework
---

# LLM Fundamentals

Before building AI agents, it helps to understand the technology that powers them: **large language models (LLMs)**. This page gives you a developer-oriented overview of what LLMs are, how they work, what they're good at, and where they fall short — so you can make informed decisions as you build agents on top of them.

> [!TIP]
> If you're already comfortable with LLMs and want to jump straight into building, skip ahead to [From LLMs to Agents](from-llms-to-agents.md).

## What is an LLM?

A large language model is a [neural network](https://en.wikipedia.org/wiki/Neural_network#In_machine_learning) trained on massive amounts of text data to predict the next token in a sequence. Through this simple training objective — *given all the previous tokens, what comes next?* — the model learns language structure and world knowledge.

At its core, an LLM is just two things:

1. **Model weights** — billions of numerical parameters learned during training that encode the model's knowledge.
2. **Architecture code** — the neural network structure (typically a [Transformer](https://en.wikipedia.org/wiki/Transformer_(deep_learning))) that runs the weights to produce output.

> [!TIP]
> We highly recommend watching Andrej Karpathy's [Deep Dive into LLMs like ChatGPT](https://www.youtube.com/watch?v=7xTGNNLPyMI), which covers how LLMs are trained, how they work internally, and what should be expected from them.

### Tokens: the building blocks

LLMs don't process raw text character by character — they work with **tokens**. A tokenizer splits input text into tokens, which are sub-word units from a fixed vocabulary. A token might be a full word (`"hello"`), part of a word (`"un"` + `"believ"` + `"able"`), a single character, or punctuation.

For example, the sentence "Tokenization is fascinating!" might break down into tokens like:

```
["Token", "ization", " is", " fascinating", "!"]
```

> [!TIP]
> Notice the spaces before some tokens — tokenization is not always word-aligned.

Each token maps to a number (an ID in the model's vocabulary), and the model operates entirely on these numbers — not on text. When the model produces output, it generates token IDs that are then decoded back into text.

The tokens above might map to the following IDs in the model's vocabulary:

```
[4421, 2860, 382, 33733, 0]
```

Understanding tokens matters because they are the unit of everything in LLMs:

- **Pricing** is typically per-token (input tokens + output tokens)
- **Context windows** are measured in tokens (not words or characters)
- **Longer prompts** use more tokens, cost more, and leave less room for the model's response

A rough rule of thumb: 1 token ≈ ¾ of a word in English.

> [!TIP]
> To see how text is tokenized, this is a useful [online tokenizer](https://platform.openai.com/tokenizer) provided by OpenAI.

### How LLMs are trained

Modern LLMs go through multiple stages of training, each building on the last to produce increasingly capable and useful models.

#### Stage 1: Pretraining

Pretraining is where the model learns the bulk of its knowledge. The model is fed massive amounts of text from the internet — books, articles, code, websites — and learns to predict the next token given all previous tokens. This stage requires enormous compute (thousands of GPUs for weeks or months) and produces a **base model**.

A base model is essentially a text-completion engine. Given a prompt, it generates plausible continuations based on patterns in the training data. However, a base model isn't particularly useful as an assistant — it may continue your text in unexpected ways, generate harmful content, or simply ramble. It doesn't follow instructions reliably.

#### Stage 2: Post-training

Post-training transforms a base model into a useful assistant. This stage happens in multiple phases:

**Supervised Fine-Tuning (SFT)** — The model is trained on curated datasets of high-quality conversations: human-written examples of ideal assistant behavior. These examples show the model *how* to follow instructions, answer questions helpfully, decline harmful requests, and format responses clearly. SFT teaches the model the role of a helpful assistant.

**Reinforcement Learning from Human Feedback (RLHF)** — After SFT, human raters compare pairs of model responses and indicate which is better. This preference data trains a reward model, which is then used with **reinforcement learning** to further tune the LLM toward responses that humans prefer. RLHF helps the model learn subtle quality distinctions that are hard to capture in static examples — like being concise vs. thorough, or knowing when to ask for clarification. This usually works in **unverifiable domains**, where there is no single correct answer, unlike problems with a clear objective or ground truth, such as arithmetic.

> [!TIP]
> For intrigued readers, please refer to OpenAI's blog post on [instruction tuning](https://openai.com/research/instruction-following) or the [paper](https://arxiv.org/abs/2203.02155).

#### Stage 3: Reasoning through reinforcement learning

More recently, reinforcement learning techniques have been applied to teach models to **reason step by step** before producing a final answer. Rather than immediately responding, these models learn to generate a chain of thought — breaking problems into sub-steps, exploring alternatives, and verifying their work.

This is the training approach behind reasoning models (such as OpenAI's o-series). The result is models that are significantly better at math, logic, coding, and complex multi-step problems, at the cost of higher latency and token usage (the reasoning steps are generated as tokens too).

> [!NOTE]
> There are many ways to achieve reasoning in LLMs. Please refer to this post for a detailed overview: [Reasoning in Large Language Models](https://magazine.sebastianraschka.com/p/understanding-reasoning-llms). Reinforcement learning is the most powerful approach as it allows the model to learn from **its own reasoning process**. This approach usually works in **verifiable domains**, such as mathematics, logic, and coding. This is why the resulting models are significantly better at these tasks.

> [!TIP]
> You don't need to understand every training detail to build agents, but knowing these stages helps explain why models behave differently. A base model completes text. An SFT + RLHF model follows instructions. A reasoning model thinks step by step. When choosing a model for your agent, these differences directly affect capability, cost, and latency.

### How inference works

When you send a request to an LLM, the model generates its response **one token at a time** through a process called **autoregressive generation**:

1. Your full prompt (system message, conversation history, user input) is converted into tokens and fed into the model.
2. The model processes all input tokens and produces a probability distribution over its vocabulary — predicting which token is most likely to come next.
3. A token is selected from that distribution (influenced by temperature and other sampling parameters).
4. That new token is **appended to the full sequence**, and the entire updated sequence is fed back into the model to generate the next token.
5. This repeats until the model produces a stop token or reaches a length limit.

This iterative process means that conceptually, the model considers the entire token sequence for every token it generates. This is why LLMs have a fixed **context window** — a maximum number of tokens the model can handle. Everything must fit: your prompt, the conversation history, any injected context, *and* the tokens the model is generating as its response.

> [!TIP]
> In practice, modern LLM inference engines use optimizations like [**KV-cache**](https://arxiv.org/pdf/2603.20397) — caching intermediate computations from previously processed tokens so that each new token doesn't require reprocessing the full sequence from scratch. This is why generating the first token (the "prefill" phase, which processes all input tokens) takes longer than generating subsequent tokens (the "decode" phase, which processes one token at a time using the cache).

```
Context window (e.g., 128K tokens)
┌────────────────────────────────────────────────────────┐
│ System      │ History │ User  │ ← Generated response → │
│ instructions│         │ input │                        │
│         (input tokens)        │    (output tokens)     │
└────────────────────────────────────────────────────────┘
```

Modern models offer context windows from 4K to over 1M tokens, but the context window is always finite. This is your working memory budget — everything the model needs to know must fit within it.

> [!IMPORTANT]
> Because inference is autoregressive (one token at a time), longer responses take proportionally longer to generate. Each token requires a full forward pass through the model. This is why **streaming** — sending tokens to the client as they're generated rather than waiting for the complete response — is a common pattern in agent applications.

## Key concepts for developers

### Chat completions: the basic API pattern

Modern LLMs are accessed through a **chat completions API** that uses a structured message format:

| Role | Purpose |
|------|---------|
| **System** | Sets the model's behavior, persona, and constraints (the "instructions") |
| **User** | The human's input or question |
| **Assistant** | The model's previous responses (for multi-turn context) |

A typical request looks like this (simplified):

```
Messages:
  [system]    "You are a helpful assistant that answers questions about weather."
  [user]      "What's the weather like in Seattle?"
```

The model processes all messages in the context window and generates the next assistant response. This stateless request-response pattern is the foundation that agents build upon.

> [!NOTE]
> Depending on the model and the API, the exact format and fields of the messages may vary. And underneath, these messages are converted into a format that may look like `<system>...</system><user>...</user><assistant>...</assistant><user>...</user><assistant>`, which will then be tokenized and processed by the model.

### Temperature and determinism

**Temperature** controls the randomness of the model's output:

- **Temperature = 0**: More deterministic — the model picks the most likely token each time
- **Temperature > 0**: More creative — the model samples from a broader distribution

For agent applications, lower temperatures (0–0.3) are typically preferred for reliable, consistent behavior. Higher temperatures (0.7–1.0) suit creative tasks.

> [!IMPORTANT]
> Even at temperature 0, LLMs are not fully deterministic. Small variations can occur due to floating-point arithmetic, batching, and infrastructure differences. Don't design systems that depend on identical output for identical input.

## What LLMs are good at

LLMs excel at tasks that involve language understanding and generation:

- **Reasoning and analysis** — breaking down problems, comparing options, explaining concepts
- **Content generation** — writing articles, emails, reports, and code
- **Summarization** — distilling long documents into concise key points
- **Translation** — converting between natural languages, or between formats (JSON ↔ prose)
- **Code generation** — writing, explaining, and debugging code across many languages
- **Classification and extraction** — categorizing text, extracting structured data from unstructured input
- **Multimodal understanding** — many modern LLMs can process images, audio, and video alongside text, enabling tasks like describing an image, transcribing speech, or analyzing visual content
- **Structured output** — generating responses in precise formats like JSON or XML, which is essential for tool calling, data extraction, and integration with downstream systems

> [!TIP]
> Multimodal capabilities work because images, audio, and other modalities can also be converted into tokens — just like text. Specialized encoders transform these inputs into token sequences that the model processes alongside text tokens in the same context window. The fundamental mechanism remains the same: everything is tokens.

## What LLMs struggle with

Understanding LLM limitations is critical for building reliable agents:

| Limitation | What it means for your agent |
|------------|------------------------------|
| **No real-time knowledge** | The model's training data has a cutoff date. It doesn't know about events after training. |
| **Hallucinations** | LLMs can generate confident but factually incorrect responses. They "dream" plausible-sounding text rather than retrieving verified facts. |
| **No persistent memory** | Each API call is stateless. The model doesn't remember previous conversations unless you include them in the context window. |
| **Limited math and logic** | While improving, LLMs can make errors in precise calculations and formal logic. |
| **Non-deterministic** | The same prompt can produce different responses across calls. |
| **No ability to act** | LLMs generate text — they can't send emails, query databases, or call APIs on their own. |

> [!NOTE]
> Many of these limitations are exactly what agents are designed to address. Tools give agents the ability to act or retrieve real-time knowledge and even run code to ground their responses, and sessions provide persistent memory. You'll see how to address each of these as you progress through this journey.

## How LLMs learn to use tools

LLMs can only generate tokens — they can't browse the web, query a database, or call an API on their own. So how do they "use" tools? The answer is surprisingly simple: **they're trained to output a special sequence of tokens that represents a tool call**, and external code interprets that output and does the actual work.

### Tool use is just token generation

Remember that an LLM generates output one token at a time. During post-training, models are fine-tuned on examples that include tool interactions. These examples teach the model a structured format — when the model determines that it needs to use a tool, instead of generating a natural language response, it generates tokens that follow a specific schema, such as:

```json
{
  "tool": "get_weather",
  "arguments": { "location": "Seattle" }
}
```

To the model, this isn't fundamentally different from generating any other text. It's still predicting the next token. But because it was trained on thousands of examples of when and how to produce these structured outputs, it learns *when* a tool would be helpful, *which* tool to use, and *what arguments* to provide — all expressed as a sequence of tokens.

> [!NOTE]
> Different model providers use different formats for tool calls (JSON function calls, XML-like tags, special tokens), but the principle is the same: the model generates structured output that signals "I want to call this tool with these arguments."

### How models learn when to call tools

During training, the model sees tool definitions included in the prompt — each tool described by a name, a description of what it does, and the parameters it accepts. The training examples demonstrate the pattern:

1. **A user asks a question** that requires external information or action.
2. **The model generates a tool call** instead of answering directly — because the training data showed that this is the correct behavior when the model doesn't have the information itself.
3. **A tool result appears in the conversation** (provided by external code during training data collection).
4. **The model generates a final response** that incorporates the tool result.

Through this training, the model learns the judgment of *when* to call a tool (vs. answering from its own knowledge), *which* tool to select from the available options, and *how* to formulate the arguments based on the user's request.

### Why this matters

Understanding that tool use is "just" token generation clarifies several important points:

- **The LLM never executes anything.** It only generates the *request*. Your application code (or an agent framework) is responsible for parsing the tool call, executing the function, and feeding the result back. This separation is a key safety boundary.
- **Tool quality depends on training.** A model's ability to use tools well depends on how thoroughly it was fine-tuned on tool-use examples. This is why some models are better at tool calling than others.
- **Tool descriptions are part of the prompt.** The tool definitions you provide consume tokens in the context window. More tools means fewer tokens available for conversation history and the model's response.
- **The model can make mistakes.** Just like it can hallucinate facts, it can generate tool calls with wrong arguments, call the wrong tool, or call a tool when it shouldn't. Guardrails and validation matter.

How this tool-calling capability gets wired into a full execution loop — where an agent iteratively calls tools, observes results, and decides what to do next — is the bridge from LLMs to agents, covered in the [next page](from-llms-to-agents.md).

## How this connects to agents

An LLM alone is a powerful but limited text-in, text-out system. To build useful applications, you need to add layers on top:

| Need | LLM alone | With Agent Framework |
|------|-----------|---------------------|
| Focused behavior | Craft system prompts manually | Agent with instructions and identity |
| Real-time data | Not available | Tools (function tools, MCP servers) |
| Take actions | Not possible | Tool calling with approval workflows |
| Memory | Re-send conversation each time | Sessions and context providers |
| Reliability | Hope the prompt works | Middleware for guardrails and overrides |

Agent Framework handles these layers so you can focus on your application logic rather than re-building LLM infrastructure.

## Learn more

- [What are Large Language Models (LLMs)?](https://azure.microsoft.com/resources/cloud-computing-dictionary/what-are-large-language-models-llms) — Microsoft Azure's overview of LLM types and use cases
- [Deep Dive into LLMs like ChatGPT](https://www.youtube.com/watch?v=7xTGNNLPyMI) — Andrej Karpathy's three-hour introduction covering how LLMs are trained, how they work, and what should be expected from them.

## Next steps

> [!div class="nextstepaction"]
> [From LLMs to Agents](from-llms-to-agents.md)
