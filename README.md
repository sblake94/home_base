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

## Running with Docker

`docker-compose.yml` builds `HomeBase.Service` (via `Dockerfile.service`) and
runs it alongside an `ollama` container:

```
docker compose up --build
```

The service listens on `http://localhost:8080` (gRPC over HTTP/2, controlled
by the `HOMEBASE_TCP_PORT` env var) in addition to its Unix socket, since
Unix sockets don't cross container boundaries. Settings and the SQLite
database persist in the `homebase-config`/`homebase-data` volumes.

Ollama itself isn't installed in the service image — it runs as the `ollama`
sibling container. On first run, edit the settings file in the
`homebase-config` volume (or exec into the container) so `Endpoint` points to
`http://ollama:11434` instead of the `http://localhost:11434` default, then
pull a model into the `ollama` container: `docker compose exec ollama ollama pull llama2`.

The Avalonia UI (`HomeBase`) only supports connecting over the Unix socket
today, so it must be run outside Docker against a locally-running
`HomeBase.Service` — it can't yet talk to the Dockerized backend over TCP.

The repository root `Dockerfile` is a separate template used by the
automated evaluation harness (fetches a pinned commit via `$REPO_URL`/
`$BASE_COMMIT`) and is not the one used by `docker-compose.yml`.


