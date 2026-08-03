# Plan: Docked Text Editor Panel with Backend-Mediated Save/Load

Add a VS-Code-like resizable/closable docking layout (via the **Dock** library, `wieslawsoltes/Dock`) to `MainWindow`, hosting the existing chat panel on the left and a new multi-tab, syntax-highlighted text editor (via **AvaloniaEdit** + TextMate) on the right. Documents are opened/saved by filesystem path, with the actual disk I/O performed by `HomeBase.Service` over a new gRPC contract — mirroring the existing chat IPC architecture (`CoreGrpcChannelFactory` / Unix socket at `${XDG_RUNTIME_DIR}/homebase/core.sock`).

User decisions confirmed via questions: backend-mediated persistence (not client-side file I/O), AvaloniaEdit code editor (not plain TextBox), full Dock-based docking layout with multi-tab documents, Save + Save As + dirty tracking.

## Steps

### Phase A — Backend document contract (parallel with Phase B)
1. Add `HomeBase.Contracts/Protos/documents.proto`: `DocumentApi` service, namespace `HomeBase.Contracts.Documents.V1`, package `homebase.documents.v1` — mirror shape of [chat.proto](HomeBase.Contracts/Protos/chat.proto):
   - `rpc OpenDocument (OpenDocumentRequest) returns (OpenDocumentResponse)` — request: `path`; response: `content`, `success`, `error_code`, `error_message`.
   - `rpc SaveDocument (SaveDocumentRequest) returns (SaveDocumentResponse)` — request: `path`, `content`; response: `success`, `error_code`, `error_message`.
2. Add `HomeBase.Core/Documents/IDocumentService.cs` + `FileDocumentService.cs`: async `ReadAsync(path)` / `WriteAsync(path, content)` wrapping `System.IO.File`, basic validation (non-empty, rooted/absolute path). Throw a `DocumentServiceException(code, message)` on failure — mirror `CoreChatException` style in [CoreChatService.cs](HomeBase/Services/ChatService/CoreChatService.cs).
3. Add `HomeBase.Service/Services/DocumentGrpcService.cs` implementing `DocumentApi.DocumentApiBase`, delegating to `IDocumentService`, catching `DocumentServiceException` and mapping to response `success=false` + code/message — mirror [ChatGrpcService.cs](HomeBase.Service/Services/ChatGrpcService.cs) structure (ctor takes service + `ILogger<T>`).
4. Register in [HomeBase.Service/Program.cs](HomeBase.Service/Program.cs): `builder.Services.AddSingleton<IDocumentService, FileDocumentService>()` and `app.MapGrpcService<DocumentGrpcService>()`, alongside existing `ChatGrpcService` registration.
5. Add xUnit tests `HomeBase.Core.Tests/FileDocumentServiceTests.cs` — temp-dir pattern identical to [SqliteConversationStoreTests.cs](HomeBase.Core.Tests/SqliteConversationStoreTests.cs) (`IDisposable`, temp dir in ctor, cleanup in `Dispose`): round-trip read/write, missing-file error path, write-creates-file/directory.

### Phase B — Docking shell (parallel with Phase A)
6. Add package refs to [HomeBase.csproj](HomeBase/HomeBase.csproj): `Dock.Avalonia`, `Dock.Model.Mvvm`, `Dock.Avalonia.Themes.Fluent` (all v12.1.0, matches Avalonia 12.1.1 already used), `Avalonia.AvaloniaEdit` (current, NOT the deprecated `AvaloniaEdit` package), `AvaloniaEdit.TextMate`, `TextMateSharp.Grammars`.
7. Add `StyleInclude` entries to [App.axaml](HomeBase/App.axaml) `<Application.Styles>`: Dock's Fluent theme include + `<StyleInclude Source="avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml" />` (confirm exact Dock theme `avares://` URI against installed package — check the Dock repo's `Notepad` sample app for the canonical setup).
8. Create `HomeBase/DependencyInjection/DockFactory.cs : Dock.Model.Mvvm.Factory` — builds initial layout: root `ProportionalDock` (horizontal) = `ToolDock` (holds chat tool, left, resizable, fixed non-closable) + `ProportionalDockSplitter` + `DocumentDock` (starts empty, documents added programmatically on Open, closable tabs).
9. Add `HomeBase/ViewModels/ChatToolViewModel.cs : Dock.Model.Mvvm.Controls.Tool` wrapping the existing `ChatViewModel` unchanged via composition (a `ChatViewModel` property) — avoids changing `ChatViewModel`'s base type.
10. Add `HomeBase/ViewModels/MainWindowViewModel.cs`: owns `IRootDock Layout` (built via injected `DockFactory`), holds `OpenDocumentCommand`/`NewDocumentCommand` (see Phase C), constructed via DI with `ChatToolViewModel` and document-open dependencies.
11. Update [MainWindow.axaml](HomeBase/MainWindow.axaml): replace `<views:ChatView />` with a `DockControl` bound to `Layout`, plus `DataTemplates` mapping `ChatToolViewModel → ChatView` and `TextDocumentViewModel → TextEditorView` (added Phase C). Add a simple top `Menu` with File > New / Open / Save / Save As.
12. Update [MainWindow.axaml.cs](HomeBase/MainWindow.axaml.cs): constructor takes `MainWindowViewModel` instead of `ChatViewModel`. Update [App.axaml.cs](HomeBase/App.axaml.cs) `OnFrameworkInitializationCompleted` to resolve `MainWindowViewModel`.
13. Update [ServiceManager.cs](HomeBase/DependencyInjection/ServiceManager.cs): register `DockFactory`, `MainWindowViewModel`, `ChatToolViewModel` as singletons alongside existing `services.AddSingleton<ChatViewModel>()` (unchanged).

### Phase C — Text editor feature (*depends on Phase A + Phase B*)
14. Add `HomeBase/Services/DocumentService/IDocumentClientService.cs` + `CoreDocumentService.cs` — gRPC client wrapper reusing the existing `CoreGrpcChannelFactory` singleton (same channel as chat, per [CoreChatService.cs](HomeBase/Services/ChatService/CoreChatService.cs) pattern): `OpenAsync(path)`, `SaveAsync(path, content)`, throws `CoreDocumentException(code, message)` on backend-reported failure.
15. Add `HomeBase/ViewModels/Documents/TextDocumentViewModel.cs : Dock.Model.Mvvm.Controls.Document` — properties `FilePath` (nullable = untitled), `Text`, `IsDirty`; commands `SaveCommand` (disabled if `!IsDirty` and path known) and `SaveAsCommand` (`RelayCommand`, reuse [RelayCommand.cs](HomeBase/Commands/RelayCommand.cs)); constructed with `IDocumentClientService`. `Title`/`Dock.Model` document title reflects filename + `*` when dirty.
16. Add `HomeBase/Views/TextEditorView.axaml` + `.axaml.cs`: hosts `AvaloniaEdit.TextEditor`; installs TextMate (`InstallTextMate(new RegistryOptions(ThemeName.DarkPlus))`, `SetGrammar` resolved via `RegistryOptions.GetLanguageByExtension(Path.GetExtension(FilePath))` on load). Since `TextEditor.Text` isn't a standard two-way bindable property, sync manually in code-behind: on `TextEditor.TextChanged`, push to `TextDocumentViewModel.Text` + set `IsDirty = true` (manual-sync style matching `ChatView.axaml.cs`'s `Messages_CollectionChanged` handler); on `DataContextChanged`, push `TextDocumentViewModel.Text` into the editor's `Document`.
17. Wire File menu / `MainWindowViewModel` commands:
    - **New**: create untitled `TextDocumentViewModel` (empty text, null path), add to `DocumentDock.VisibleDockables`, focus it.
    - **Open**: `TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync` picks a path client-side (UI-only, no file read) → call `CoreDocumentService.OpenAsync(path)` → create `TextDocumentViewModel` with returned content → add to dock.
    - **Save**: if `FilePath` set, call `CoreDocumentService.SaveAsync(FilePath, Text)`, clear `IsDirty`; else fall back to Save As.
    - **Save As**: `StorageProvider.SaveFilePickerAsync` for a new path → `SaveAsync` → update `FilePath`, clear `IsDirty`, update tab title.
    - Closing a dirty tab (Dock's built-in close) — add a confirmation via `Dock.Model` document-closing hook or a simple check in the close command if straightforward; otherwise note as a known gap (see Further Considerations).

## Relevant files
- `HomeBase.Contracts/Protos/documents.proto` — new
- `HomeBase.Core/Documents/IDocumentService.cs`, `FileDocumentService.cs`, `DocumentServiceException.cs` — new
- `HomeBase.Service/Services/DocumentGrpcService.cs` — new; [HomeBase.Service/Program.cs](HomeBase.Service/Program.cs) — registration
- `HomeBase.Core.Tests/FileDocumentServiceTests.cs` — new
- [HomeBase/HomeBase.csproj](HomeBase/HomeBase.csproj), [HomeBase/App.axaml](HomeBase/App.axaml) — package refs + styles
- `HomeBase/DependencyInjection/DockFactory.cs`, `HomeBase/ViewModels/MainWindowViewModel.cs`, `HomeBase/ViewModels/ChatToolViewModel.cs` — new
- [HomeBase/MainWindow.axaml](HomeBase/MainWindow.axaml), [HomeBase/MainWindow.axaml.cs](HomeBase/MainWindow.axaml.cs), [HomeBase/App.axaml.cs](HomeBase/App.axaml.cs) — layout + DI wiring changes
- [HomeBase/DependencyInjection/ServiceManager.cs](HomeBase/DependencyInjection/ServiceManager.cs) — new registrations
- `HomeBase/Services/DocumentService/IDocumentClientService.cs`, `CoreDocumentService.cs` — new
- `HomeBase/ViewModels/Documents/TextDocumentViewModel.cs`, `HomeBase/Views/TextEditorView.axaml(.cs)` — new

## Verification
1. `dotnet build HomeBase.slnx` succeeds after each phase.
2. `dotnet test HomeBase.Core.Tests/HomeBase.Core.Tests.csproj` passes, including new `FileDocumentServiceTests`.
3. Manual: run `HomeBase.Service` then `HomeBase` UI (per repo memory run order) — confirm chat still works inside its dockable pane; resize the splitter.
4. Manual: File > New, type text, Save As to a new path, confirm file appears on disk with correct content; File > Open that same file in a second tab, confirm content matches; edit + Save, confirm round-trip; close a tab.
5. Manual: open a `.cs` and a `.md` file, confirm distinct TextMate syntax highlighting applies per tab.

## Decisions
- Backend-mediated persistence (gRPC via `HomeBase.Service`) chosen over client-side file I/O, per user, for architectural consistency with chat; no additional path-sandboxing added — same-user, local-only Unix-socket trust boundary as chat (`UserRead|UserWrite|UserExecute`-only dir perms already in place), so this isn't a new attack surface.
- AvaloniaEdit (with TextMate grammars) chosen over plain TextBox, per user, for syntax highlighting/line numbers.
- Full Dock-based docking layout (resizable/closable panes, multi-tab documents) chosen over a fixed split, per user.
- Save + Save As + dirty-state tracking included, per user.
- `ChatViewModel` left unmodified; wrapped by new `ChatToolViewModel : Tool` (composition) to avoid touching its base type or existing behavior.
- No SQLite persistence / recent-files list for documents — files read/written directly at their real path only (not requested; could be a future addition reusing `SqliteConversationStore`'s pattern).
- No new UI test project — matches existing convention of only Core-layer (`HomeBase.Core.Tests`) automated tests.

## Further Considerations
1. Exact Dock Fluent-theme `StyleInclude` URI and precise `Dock.Model.Mvvm` namespaces/class shapes should be cross-checked against the Dock repo's bundled **Notepad** sample app (a real docking-text-editor reference) during implementation — low risk, build-time detail only.
2. Confirming a dirty document's tab close (Dock's built-in close button) should prompt "Save changes?" — Dock supports a document-closing hook (`IDockable`/factory `OnDockableClosing`-style callback); needs implementation-time verification of the exact hook API in the installed Dock version. If not straightforwardly available, ship v1 without the close-confirmation prompt and note it as a follow-up.
