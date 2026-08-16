# Task: Rich chat-event protocol v2
## Tool-call and lifecycle visibility through the whole pipe

### Task statement: 
Evolve the chat streaming protocol so the UI can show what the agent is doing: intercept agent tool invocations in Core (wrapping the AIFunctionFactory tools) and emit new ToolCallStarted/ToolCallCompleted (tool name, arguments summary, duration, success) and AssistantStarted events; extend the ChatEvent proto oneof backward-compatibly; map them in the host; replace the UI IChatService's bare IAsyncEnumerable<string> with a typed client-side event model consumed by ChatViewModel (rendering tool activity distinctly via ChatMessageTemplateSelector), updating both CoreChatService and DummyChatService; require that an old client ignoring unknown oneof cases still works (token-only degradation).

### Target area: 
HomeBase.Core/Chat/{ChatStreamEvent,LocalHostConversationService}.cs + a new tool-instrumentation wrapper in HomeBase.Core/Tools/; HomeBase.Contracts/Protos/chat.proto; HomeBase.Service/Services/ChatGrpcService.cs; HomeBase/Services/ChatService/{IChatService,CoreChatService,DummyChatService}.cs; HomeBase/ViewModels/{ChatViewModel,ChatMessageViewModel}.cs; HomeBase/Selectors/ChatMessageTemplateSelector.cs; HomeBase/Views/ChatView.axaml.

### Expected footprint: 
~550–700 LOC across 11–14 files. Breadth comes from redefining the event vocabulary that four layers share, plus changing the client-side abstraction (IAsyncEnumerable<string> → typed events) which ripples through every IChatService implementer and consumer. Clearly hits the file-spread target and likely LOC too.

### Why challenging: 
Instrumenting tool calls requires understanding how Microsoft.Agents.AI invokes AIFunctions and wrapping them without breaking the existing wire-protocol tests that assert exact tool schemas in the Ollama request body; the interface change is a deliberate breaking refactor that must be carried consistently through CoreChatService, DummyChatService, ChatViewModel, and the host mapper's exhaustive switch (which throws on unknown events — a new event type missed in the mapper is a runtime failure the compiler won't catch). Ordering guarantees (ToolCallStarted before its tokens' continuation, Completed last) must be asserted.

### Golden-test strategy: 
#### F2P: 
Core tests using the existing tool-round-trip fixture (queued tool-call response → tool output → final response) asserting the emitted ChatStreamEvent sequence contains correctly-ordered ToolCall events with the right tool name and success flag, plus error-path tests (tool throws → ToolCallCompleted(success=false) and stream continues/fails per spec); ChatGrpcService.ToContractEvent mapping tests covering every event type. 
#### P2P: 
Both existing wire-protocol tests (schemas + "role":"tool" round trip) are the key regression anchors; validation-failure and store/document/settings tests.

### Offline/build risks: 
Verify how tools can be wrapped in Microsoft.Agents.AI 1.17 (e.g., delegating AIFunction or middleware) before committing — if the framework offers no interception seam, the wrapper must be hand-rolled around the delegates passed to AIFunctionFactory.Create, which is doable but should be confirmed against the cached package. UI-layer (ChatViewModel) assertions are risky offline due to Dispatcher.UIThread; keep F2P checks in Core/Service and treat UI changes as compile-checked.