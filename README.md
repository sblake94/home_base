# home_base

## Solution layout

- `HomeBase` — Avalonia desktop UI. Talks to the backend over gRPC.
- `HomeBase.Contracts` — shared protobuf/gRPC contract (`Protos/chat.proto`).
- `HomeBase.Core` — conversation orchestration (Ollama), settings, and SQLite persistence.
- `HomeBase.Service` — gRPC host that exposes `HomeBase.Core` over a Unix domain socket.

Build everything: `dotnet build HomeBase.slnx`

## Running locally (development)

Run the backend and the UI in separate terminals:

```
dotnet run --project HomeBase.Service/HomeBase.Service.csproj
dotnet run --project HomeBase/HomeBase.csproj
```

The backend listens on `$XDG_RUNTIME_DIR/homebase/core.sock` (falls back to a
temp directory if `XDG_RUNTIME_DIR` is unset). The UI connects to the same
path automatically; if the backend isn't running, the chat input is disabled
and a status message is shown.

Settings (Ollama endpoint/model/system prompt) are owned by `HomeBase.Core`
and stored at `$XDG_CONFIG_HOME/HomeBase/settings.json` (default
`~/.config/HomeBase/settings.json`), created with defaults on first run. If an
older `~/.local/share/HomeBase/local_settings.json` from a previous UI-only
version exists, its values are migrated in automatically.

Conversation/message history is persisted to SQLite at
`$XDG_DATA_HOME/HomeBase/homebase.db` (default `~/.local/share/HomeBase/homebase.db`).

## Installing the backend as a systemd user service

```
dotnet publish HomeBase.Service/HomeBase.Service.csproj -c Release -o ~/.local/share/homebase/core
mkdir -p ~/.config/systemd/user
cp deploy/systemd/homebase-core.service ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now homebase-core.service
journalctl --user -u homebase-core.service -f
```

This unit starts the process directly; `HomeBase.Service` creates and binds
its own socket at startup. It does not implement systemd fd-based socket
activation (a paired `.socket` unit) — that remains a possible future
enhancement.

