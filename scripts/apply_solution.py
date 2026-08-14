#!/usr/bin/env python3

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
import difflib
import sys


COMMIT_SHA = "e89d2aea7c3021a9869f7a3bc1582c806662753c"


@dataclass(frozen=True)
class Transform:
	name: str
	before: str
	after: str
	count: int = 1


@dataclass(frozen=True)
class FileSpec:
	path: str
	transforms: tuple[Transform, ...]
	after_markers: tuple[str, ...]


@dataclass
class FileResult:
	path: str
	status: str
	message: str
	diff_text: str = ""


def _apply_transform(text: str, transform: Transform) -> tuple[str, str | None]:
	def variant(value: str, mode: str) -> str:
		if mode == "exact":
			return value
		if mode == "tabs_to_spaces":
			return value.replace("\t", "    ")
		if mode == "spaces_to_tabs":
			return value.replace("    ", "\t")
		raise ValueError(f"unknown mode: {mode}")

	modes = ("exact", "tabs_to_spaces", "spaces_to_tabs")
	diagnostics: list[str] = []

	for mode in modes:
		before = variant(transform.before, mode)
		after = variant(transform.after, mode)
		before_count = text.count(before)

		if after:
			after_count = text.count(after)

			# If the post-state exists already, this transform is a no-op.
			if after_count >= transform.count and (before_count == 0 or before in after):
				return text, None

			needed = transform.count - after_count
			if needed <= 0:
				return text, None

			if before_count == needed:
				updated = text
				for _ in range(needed):
					updated = updated.replace(before, after, 1)
				return updated, None

			diagnostics.append(
				f"mode={mode}: before={before_count}, after={after_count}, needed={needed}"
			)
		else:
			if before_count == 0:
				continue

			if before_count == transform.count:
				updated = text
				for _ in range(transform.count):
					updated = updated.replace(before, "", 1)
				return updated, None

			diagnostics.append(
				f"mode={mode}: before={before_count}, expected={transform.count}"
			)

	if not diagnostics and transform.after == "":
		# Nothing to remove in any indentation variant.
		return text, None

	return text, (
		f"transform '{transform.name}' could not be safely applied; "
		+ "; ".join(diagnostics)
	)


def _atomic_write(path: Path, content: str) -> None:
	tmp_path = path.with_suffix(path.suffix + ".tmp.apply_solution")
	tmp_path.write_text(content, encoding="utf-8", newline="")
	tmp_path.replace(path)


def _make_unified_diff(path: str, original: str, updated: str) -> str:
	lines = difflib.unified_diff(
		original.splitlines(keepends=True),
		updated.splitlines(keepends=True),
		fromfile=f"a/{path}",
		tofile=f"b/{path}",
	)
	return "".join(lines)


def _apply_file(repo_root: Path, spec: FileSpec) -> FileResult:
	target = repo_root / spec.path
	if not target.exists():
		return FileResult(spec.path, "error", "target file does not exist")

	original = target.read_text(encoding="utf-8")
	updated = original
	errors: list[str] = []

	for transform in spec.transforms:
		updated, err = _apply_transform(updated, transform)
		if err is not None:
			errors.append(err)
			break

	if errors:
		return FileResult(spec.path, "mismatch", "; ".join(errors))

	missing_after = [marker for marker in spec.after_markers if marker not in updated]
	if missing_after:
		return FileResult(
			spec.path,
			"mismatch",
			f"post-check failed, missing {len(missing_after)} after marker(s)",
		)

	if updated == original:
		return FileResult(spec.path, "already-applied", "file already contains post-commit state")

	_atomic_write(target, updated)
	patch = _make_unified_diff(spec.path, original, updated)
	return FileResult(spec.path, "updated", "file updated", patch)


def _build_specs() -> tuple[FileSpec, ...]:
	tests_before_httpclient = """new ServiceCollection()
				.AddSingleton(new HttpClient())
				.BuildServiceProvider());"""

	tests_after_httpclient = """new ServiceCollection().BuildServiceProvider());"""

	local_service_using_before = """using HomeBase.Core.Tools;
using HomeBase.SharedLib.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using OllamaChat = OllamaSharp.Chat;
using Microsoft.Extensions.DependencyInjection;"""

	local_service_using_after = """using HomeBase.Core.Tools;
using HomeBase.SharedLib.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using OllamaSharp;"""

	local_service_constructor_before = """        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
		if(httpClient is not null)
		{
			_httpClient = httpClient;
		}"""

	local_service_constructor_after = """        _httpClient = serviceProvider.GetService<HttpClient>()
			?? serviceProvider.GetKeyedService<HttpClient>("OllamaClient");

		if (_httpClient is not null)
		{
			_log.LogInfo($"Using provided HttpClient at {_httpClient.BaseAddress}");
		}"""

	local_service_send_before = """    public async IAsyncEnumerable<ChatStreamEvent> SendMessageAsync(
		string conversationId,
		string content,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(conversationId))
		{
			yield return new ChatFailed("invalid_conversation", "A conversation ID is required.");
			yield break;
		}

		if (string.IsNullOrWhiteSpace(content))
		{
			yield return new ChatFailed("invalid_message", "Message content is required.");
			yield break;
		}

		var state = _conversations.GetOrAdd(conversationId, _ => CreateConversation());
        
		state.Tools.Clear();
		state.Tools.AddRange(
		[
			new ListDocumentNamesTool(),
			new ReadDocumentTool()
		]);
        
		state.Chat.OnToolCall += HandleToolCall;
		state.Chat.OnToolResult += HandleToolResult;

		await state.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		var messageId = Guid.NewGuid().ToString("N");
		_store.RecordUserMessage(conversationId, content);
		_store.BeginAssistantMessage(conversationId, messageId);

		var accumulated = new StringBuilder();
		var cancelled = false;
		Exception? failure = null;

		foreach(var tool in state.Tools)
		{
			if (tool is not Tool chatTool)
			{
				_log.LogWarning($"Tool {tool.GetType().Name} is not compatible with {nameof(Tool)} and will be skipped.");
				continue;
			}

			_log.LogInfo($"{chatTool.Function?.Name ?? "UnKnown"}\\t\\t[purple]{chatTool.Function?.Description ?? "No description"}[/]");
		}


		var enumerator = state.Chat.SendAsync(content, state.Tools, null, null, cancellationToken).GetAsyncEnumerator(cancellationToken);
		try
		{
			yield return new AssistantStarted(messageId);

			while (true)
			{
				string? token = null;
				var hasNext = false;

				// Isolated from the yields below: an iterator cannot yield inside a try/catch (CS1626).
				try
				{
					hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
					if (hasNext)
					{
						token = enumerator.Current;
					}
				}
				catch (OperationCanceledException)
				{
					cancelled = true;
				}
				catch (Exception ex)
				{
					failure = ex;
				}

				if (cancelled || failure is not null || !hasNext)
				{
					break;
				}

				accumulated.Append(token);
				yield return new AssistantToken(token!);
			}
		}
		finally
		{
			await enumerator.DisposeAsync().ConfigureAwait(false);
			state.Chat.OnToolCall -= HandleToolCall;
			state.Chat.OnToolResult -= HandleToolResult;
			state.SendLock.Release();
		}

		if (cancelled)
		{
			_store.MarkIncomplete(messageId, accumulated.ToString());
			yield break;
		}

		if (failure is not null)
		{
			_store.MarkFailed(messageId, accumulated.ToString());
			yield return new ChatFailed("ollama_unreachable", "Unable to reach the Ollama backend.");
			yield break;
		}

		_store.MarkCompleted(messageId, accumulated.ToString());
		yield return new AssistantCompleted(messageId);
	}"""

	local_service_send_after = """    public async IAsyncEnumerable<ChatStreamEvent> SendMessageAsync(
		string conversationId,
		string content,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if(string.IsNullOrWhiteSpace(conversationId))
		{
			yield return new ChatFailed("invalid_conversation", "The conversation ID cannot be null.");
			yield break;
		}

		if(string.IsNullOrWhiteSpace(content))
		{
			yield return new ChatFailed("invalid_message", "Message content is required.");
			yield break;
		}

		var state = _conversations.GetOrAdd(conversationId, _ => CreateConversation());

		await state.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		var messageId = Guid.NewGuid().ToString();

		var accumulated = new StringBuilder();
        
		_log.LogInfo($"Sending message to conversation {conversationId} with message ID {messageId}: {content}");

		try
		{
			await foreach (var token in state.Agent.RunStreamingAsync(content, state.Session).ConfigureAwait(false))
			{
				yield return new AssistantToken(token.Text);
				accumulated.Append(token.Text);
			}
            
			yield return new AssistantCompleted(messageId);
			_log.LogInfo($"Message {messageId} sent successfully to conversation {conversationId}. Accumulated response: {accumulated}");
		}
		finally
		{
			state.SendLock.Release();
		}
	}"""

	local_service_handle_tool_call_before = """    private void HandleToolCall(object? sender, Message.ToolCall e)
	{
        
	}
"""

	local_service_handle_tool_result_before = """    private void HandleToolResult(object? sender, ToolResult e)
	{
		_log.LogInfo($"Tool result received: {e.Tool.GetType().Name} - {e.Result}");
	}
"""

	local_service_create_before = """    private ConversationState CreateConversation()
	{
		var settings = _settings.GetOllamaSettings();

		IOllamaApiClient client;
        
		if (_httpClient != null)
		{   
			// Use the provided HttpClient for testing purposes
			client = new OllamaApiClient(_httpClient, settings.Model);
		}
		else
		{
			client = new OllamaApiClient(settings.Endpoint, settings.Model);
		}

		_log.LogInfo($"Created new Ollama conversation with endpoint {settings.Endpoint} and model {settings.Model}");
		_log.LogInfo($"System prompt: {settings.SystemPrompt}");
        
		return new ConversationState(new OllamaChat(client, settings.SystemPrompt));
	}"""

	local_service_create_after = """    private ConversationState CreateConversation()
	{
		var settings = _settings.GetOllamaSettings();
		var endpoint = settings.Endpoint;
		var modelName = settings.Model;

		var client = _httpClient ?? throw new ArgumentNullException(nameof(_httpClient), "HttpClient must be provided to create a conversation.");
		if (client.BaseAddress is null)
		{
			client.BaseAddress = new Uri(endpoint);
		}

		AIAgent agent = new OllamaApiClient(client, modelName)
			.AsAIAgent(
				name: "Terry",
				instructions: "You are a helpful assistant.", 
				loggerFactory: _loggerFactory,
				tools: 
				[
					AIFunctionFactory.Create(DocumentTools.ReadDocument, nameof(DocumentTools.ReadDocument), "Reads the content of a document by its name."),
					AIFunctionFactory.Create(DocumentTools.ListDocumentNames, nameof(DocumentTools.ListDocumentNames), "Lists the names of all the documents available in the document service.")
				]); 

		return new ConversationState(agent);
	}"""

	local_service_state_before = """    private sealed class ConversationState
	{
		public ConversationState(OllamaChat chat)
		{
			Chat = chat;
		}

		public OllamaChat Chat { get; }
		public SemaphoreSlim SendLock { get; } = new(1, 1);
		public List<Tool> Tools { get; } = new();
	}"""

	local_service_state_after = """    private sealed class ConversationState
	{
		public ConversationState(AIAgent agent)
		{
			Agent = agent;
		}

		public AIAgent Agent { get; }
		public AgentSession Session { get; set; }
		public SemaphoreSlim SendLock { get; } = new(1, 1);
	}"""

	document_tools_using_before = """using HomeBase.Core.Documents;
using HomeBase.SharedLib.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;"""

	document_tools_using_after = """using System.ComponentModel;
using HomeBase.Core.Documents;
using Microsoft.SemanticKernel;"""

	document_tools_list_before = """    [OllamaTool]
	public static object ListDocumentNames()"""

	document_tools_list_after = """    [KernelFunction("ListDocumentNames")]
	[Description("Lists the names of all the documents available in the document service.")]
	public static IEnumerable<string> ListDocumentNames()"""

	document_tools_read_before = """    [OllamaTool]
	public static string ReadDocument(string documentName)"""

	document_tools_read_after = """    [KernelFunction("ReadDocument")]
	[Description("Reads the content of a document by its name.")]
	public static string ReadDocument(string documentName)"""

	program_using_before = """using HomeBase.SharedLib.Logging;
using HomeBase.SharedLib.Logging.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;"""

	program_using_after = """using HomeBase.SharedLib.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using HomeBase.SharedLib.Logging.Http;
using System.Security.Cryptography;"""

	program_services_before = """builder.Services.AddSingleton(sp => new CoreSettings(sp.GetRequiredService<ICustomLoggerFactory>()));
builder.Services.AddSingleton(sp => new HttpClient(new LoggingHandler(sp.GetRequiredService<ICustomLoggerFactory>(), new HttpClientHandler()))
{
	BaseAddress = new Uri(sp.GetRequiredService<CoreSettings>().GetOllamaSettings().Endpoint),
});"""

	program_services_after = """
builder.Services.AddKeyedSingleton("OllamaClient", (sp, key) => 
new HttpClient(new LoggingHandler(sp.GetRequiredService<ICustomLoggerFactory>(), new HttpClientHandler()))
{
	BaseAddress = new Uri(sp.GetRequiredService<CoreSettings>().GetOllamaSettings().Endpoint),
});"""

	return (
		FileSpec(
			path="HomeBase.Core.Tests/Chat/LocalHostConversationServiceTests.cs",
			transforms=(
				Transform(
					name="add_servicecollection_field",
					before="private readonly string _tempDirectory;",
					after="private readonly string _tempDirectory;\n    private readonly IServiceCollection _serviceCollection;",
				),
				Transform(
					name="remove_test_httpclient_registration",
					before=tests_before_httpclient,
					after=tests_after_httpclient,
					count=2,
				),
			),
			after_markers=(
				"private readonly IServiceCollection _serviceCollection;",
				"new ServiceCollection().BuildServiceProvider());",
			),
		),
		FileSpec(
			path="HomeBase.Core/Chat/LocalHostConversationService.cs",
			transforms=(
				Transform("update_using_block", local_service_using_before, local_service_using_after),
				Transform("update_httpclient_resolution", local_service_constructor_before, local_service_constructor_after),
				Transform("rewrite_sendmessage_flow", local_service_send_before, local_service_send_after),
				Transform("remove_handle_tool_call", local_service_handle_tool_call_before, ""),
				Transform("remove_handle_tool_result", local_service_handle_tool_result_before, ""),
				Transform("rewrite_createconversation", local_service_create_before, local_service_create_after),
				Transform("rewrite_conversation_state", local_service_state_before, local_service_state_after),
			),
			after_markers=(
				"using Microsoft.Agents.AI;",
				"state.Agent.RunStreamingAsync(content, state.Session)",
				"AIFunctionFactory.Create(DocumentTools.ReadDocument",
				"public AgentSession Session { get; set; }",
			),
		),
		FileSpec(
			path="HomeBase.Core/Tools/DocumentTools.cs",
			transforms=(
				Transform("update_usings", document_tools_using_before, document_tools_using_after),
				Transform("class_static_to_instance", "public static class DocumentTools", "public class DocumentTools"),
				Transform("list_tool_attributes", document_tools_list_before, document_tools_list_after),
				Transform("read_tool_attributes", document_tools_read_before, document_tools_read_after),
			),
			after_markers=(
				"using Microsoft.SemanticKernel;",
				"[KernelFunction(\"ListDocumentNames\")]",
				"[KernelFunction(\"ReadDocument\")]",
				"public class DocumentTools",
			),
		),
		FileSpec(
			path="HomeBase.Service/Program.cs",
			transforms=(
				Transform("update_using_block", program_using_before, program_using_after),
				Transform("use_keyed_ollama_client", program_services_before, program_services_after),
			),
			after_markers=(
				"using Microsoft.Extensions.DependencyInjection;",
				"builder.Services.AddKeyedSingleton(\"OllamaClient\"",
			),
		),
	)


def _write_artifact(repo_root: Path, results: list[FileResult]) -> Path:
	out_dir = repo_root / "scripts" / "tmp"
	out_dir.mkdir(parents=True, exist_ok=True)

	stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
	out_path = out_dir / f"apply_solution_{stamp}_{COMMIT_SHA[:8]}.patch"

	lines = [
		f"# apply_solution audit artifact\n",
		f"# commit: {COMMIT_SHA}\n",
		f"# generated_utc: {stamp}\n",
		"\n",
	]

	for result in results:
		lines.append(f"# file: {result.path}\n")
		lines.append(f"# status: {result.status}\n")
		lines.append(f"# message: {result.message}\n")
		lines.append("\n")
		if result.diff_text:
			lines.append(result.diff_text)
			if not result.diff_text.endswith("\n"):
				lines.append("\n")
			lines.append("\n")

	_atomic_write(out_path, "".join(lines))
	return out_path


def main() -> int:
	repo_root = Path(__file__).resolve().parents[1]
	specs = _build_specs()

	print(f"Applying hard-coded changes for commit {COMMIT_SHA}")
	print(f"Repository root: {repo_root}")

	results: list[FileResult] = []
	for spec in specs:
		result = _apply_file(repo_root, spec)
		results.append(result)
		print(f"[{result.status}] {result.path} - {result.message}")

	artifact = _write_artifact(repo_root, results)
	print(f"Audit artifact written: {artifact}")

	updated = sum(1 for r in results if r.status == "updated")
	already = sum(1 for r in results if r.status == "already-applied")
	failed = sum(1 for r in results if r.status in {"mismatch", "error"})

	print("Summary:")
	print(f"  updated: {updated}")
	print(f"  already-applied: {already}")
	print(f"  failed: {failed}")

	return 0 if failed == 0 else 1


if __name__ == "__main__":
	try:
		raise SystemExit(main())
	except KeyboardInterrupt:
		print("Interrupted by user.", file=sys.stderr)
		raise SystemExit(130)
