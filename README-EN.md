# Minecraft Server Launcher

[简体中文](README.md) | **English** | [中文副本](README-CH.md)

An [Avalonia](https://avaloniaui.net/)-based desktop launcher / manager for Minecraft servers.
It is the **graphical front end of MSL-CLI**: it shares the same commands, the same configuration
file and the same log directory as the command-line version, so servers registered from the CLI can
be managed directly in the GUI.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0.5-8B44AC)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

![Main page](.shots/page-Main.png)

---

## Table of Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Usage](#usage)
- [Command Reference](#command-reference)
- [AI and Agent](#ai-and-agent)
- [Configuration and Logs](#configuration-and-logs)
- [Project Layout](#project-layout)
- [Building from Source](#building-from-source)
- [License](#license)

---

## Features

### Server management

- **Multiple instances**: register any number of server directories with `$server add <name> <path>`;
  `%VAR%` environment variables are expanded in paths.
- **Process control**: one-click start and graceful stop. `stop -f` skips the graceful shutdown and
  terminates the whole process tree, so no orphan `java` process is left behind.
- **Launch arguments**: built-in `javaPath` / `jvmArgs` / `jarArgs` / `appendArgs` models.
  The launcher parses an existing `start.bat` / `start.sh` and can rebuild the launch line as
  `{javaPath} @user_jvm_args.txt {jarArgs} {appendArgs}`.
- **Console**: real-time per-server output with hide/show (`hd` / `uhd`), highlighted instance
  (`hl`) and output buffer reading (`buf read`, buffer capped at 512 KB).
- **Status monitoring**: instances are polled at a configurable interval (2500 ms by default,
  valid range 250–60000 ms). Player counts come from the Minecraft Query protocol; CPU and memory
  come from the server process.
- **World backups**: `$server bp <name> [note]`.
- **List checks**: whitelist / OP / banned players / banned IPs in one command
  (`ck wl|op|bp|bip`).
- **Server configuration**: edit `server.properties` directly in the GUI, or use
  `$server cfg get|getall|set|rm`.
- **File operations inside server directories**: read / write / append / delete / copy / move / list
  (`$server file ...`).
- **Instance protection**: a locked instance (`lk`) rejects `run` / `stop` / `send` / `rm` / `del`;
  deleting a directory requires an explicit `confirm`.

### Console and commands

- Input starting with `$` is executed as a **command**; anything else is sent to the currently
  selected **server** or **AI** target.
- The target is chosen by the input prefix (`服务器-` / `AI-`); an **Agent** checkbox is available for
  AI targets.
- The GUI and the CLI share the same commands, configuration and logs, so both entry points can be
  mixed freely.
- Console text and log colours mirror the CLI's ANSI colours one-to-one.

### AI integration

- Multiple **OpenAI-compatible** service instances, added / removed at runtime from the GUI
  (`$ai add` / `$ai rm`).
- **Chat mode**: plain Q&A with no tools attached — the model never executes commands.
- **Agent mode**: the model can call the `request_permission` / `execute_command` / `sleep` tools to
  finish multi-step tasks. Every command must be approved by the operator through the
  **permission gateway**; when approval cannot be obtained, the command is refused.
- **Permission whitelist**: `$ai perm` lists, adds or removes authorised scopes, or forces
  per-call confirmation for a scope.
- **Context compression**: once the estimated token count exceeds the threshold (32000 by default),
  earlier execution history is summarised first; the maximum iteration count defaults to 16.
- **In-game chat**: players can send `$chat` / `$agent` / `@ai` in the Minecraft chat to talk to the
  AI, and permission questions can be answered in game (with anti-spoofing handling).

### Other

- If the configuration file cannot be read, the launcher falls back to defaults without modifying
  the original file, and keeps a `.bak` backup.
- The global AI switch, the default AI instance and the status refresh interval can all be changed
  at runtime (`$app cfg` / `$ai cfg`).
- Everything is written to the on-screen log and to a file log under `%APPDATA%\MSL_CLI`.

---

## Screenshots

| Page | Screenshot | Description |
| --- | --- | --- |
| Main | ![Main](.shots/page-Main.png) | Server instance cards (status, players, CPU / memory) on the left, console and logs on the right |
| Server Config | ![Server Config](.shots/page-ServerConfig.png) | One panel per instance: launch arguments and `server.properties` editing |
| AI Config | ![AI Config](.shots/page-AiConfig.png) | Address / model / key / prompts and permissions for each AI instance |
| App Settings | ![App Settings](.shots/page-AppSettings.png) | Global switches, refresh interval, configuration file path |
| About | ![About](.shots/page-About.png) | Version, runtime, data scale and diagnostic info |

> Screenshots live in [`.shots/`](.shots/) and are produced by
> [`.shots/capture-pages.ps1`](.shots/capture-pages.ps1).

---

## Requirements

| Item | Requirement |
| --- | --- |
| OS | Windows 10/11 (primary development and verification platform); Avalonia itself is cross-platform |
| Runtime | [.NET 10.0](https://dotnet.microsoft.com/download) (target framework `net10.0`) |
| Dependencies | Avalonia 12.0.5, CommunityToolkit.Mvvm 8.4.1 (restored by NuGet) |
| Running servers | You supply the Java runtime and the Minecraft server files (this tool does not download them) |
| Optional | `enable-query` must be set in `server.properties` for player counts to be read |

---

## Quick Start

1. Download / build and run `MinecraftServerLauncher.exe`.
2. Register an existing server directory in the console:

   ```text
   $server add survival D:\mc\survival
   ```

3. Start it:

   ```text
   $server run survival
   ```

4. The instance card appears on the left; type into the console (with the `服务器-` prefix) to send
   commands to the server.
5. For AI features, add an OpenAI-compatible instance on the **AI Config** page, then either
   `$ai default <name>` or run a task directly with `$ai agent <task description>`.

> If the launch arguments do not match, inspect them with `$server arg get <name>` and change them
> with `$server arg set <name> jvmArgs -Xms2G -Xmx4G`.

---

## Usage

An initial page can be selected on start-up (page names are case-insensitive):

```powershell
MinecraftServerLauncher.exe --page Main          # main page (default)
MinecraftServerLauncher.exe --page ServerConfig  # server configuration
MinecraftServerLauncher.exe --page AiConfig      # AI configuration
MinecraftServerLauncher.exe --page AppSettings   # application settings
MinecraftServerLauncher.exe --page About         # about
```

Console input semantics:

| Input | Behaviour |
| --- | --- |
| `$server ls` | Starts with `$` → executed as a command (identical to the CLI) |
| `list` | No `$` + `服务器-` target → sent to the server as a Minecraft command |
| `how is the server doing?` | No `$` + `AI-` target → sent to the AI as a chat message |
| Send with **Agent** checked | Dispatched as an Agent task; the model may run multi-step commands (each needs approval) |

---

## Command Reference

Commands are identical to MSL-CLI; use `$help [command]` for the detailed usage of a single command.

| Command | Description |
| --- | --- |
| `$list` | List command names only (no descriptions) |
| `$help [command]` | Show help; without arguments lists every command and its description |
| `$server ...` | Server instance management (register, start/stop, config, backup, files, status…) |
| `$ai ...` | AI chat / agent execution, AI instance and permission management |
| `$app ...` | Application-level actions: run system commands, read/write global config, exit / reload |
| `$file ...` | File operations limited to whitelisted directories; supports `%appdata%` and `%<server>%` placeholders |

Main `$server` actions:

```text
add <name> <path>                        register a new server
rm <name>                                remove the config reference only (keep the directory)
del <name> confirm                       remove the reference and delete the whole directory (irreversible)
lk|ulk <name>                            lock / unlock
hd|uhd <name>                            hide / show console output of the instance
hl [name]                                show / switch the highlighted server
file -r|-w|-a|-rm|-cp|-mv|-ls ...        file operations inside the server directory
cfg get|getall|set|rm [name] [key] [value]   read/write server.properties
arg get|set|rm <name> <arg> [value...]   launch arguments (javaPath/jvmArgs/jarArgs/appendArgs)
ck wl|op|bp|bip <name|all> [player]      list checks (whitelist/OP/ban/ban-ip)
buf read|update <name>                   read / read-and-clear the output buffer
ls                                       list all servers and their status
bp <name> [note]                         back up the world directory
query <name|all>                         query information (Query protocol)
status [name|all]                        process status
stop <name|all> [-f]                     stop (-f forces termination)
run <name>                               start
send <name|all> <command>                send a Minecraft command
```

---

## AI and Agent

```text
$ai chat|agent [config] <message>   chat / agent execution (defaults to DefaultAIConfig)
$ai default [config]                show / set the default AI config
$ai add <config> <Url> <Model> [ApiKeyEnv]
$ai rm <config>
$ai cfg get [path] | getall | set <path> <value> | rm <path>
$ai perm [get|add|rm|ask|noask] [scope]
```

- **Chat mode** attaches no tools: the model only answers and never runs commands.
- In **Agent mode** the model can only run `$`-prefixed commands through `execute_command`, and only
  after obtaining operator approval via `request_permission`; scopes on the whitelist run directly.
- A permission scope is a command name or a "command + sub-action" pair (for example `$server` or
  `$server run`), and can be switched between `ask` (ask every time) and `noask`.
- API keys are read from environment variables by default (set the variable name with the `ApiKeyEnv`
  argument of `$ai add`), or can be stored in the configuration directly.

In game, a player can type in the chat box:

```text
$chat hello                      # chat with the default AI
$agent make it sunny             # dispatch an Agent task
@ai <config> chat|agent content  # target a specific AI instance
```

---

## Configuration and Logs

| Path | Content |
| --- | --- |
| `%APPDATA%\MSL_CLI\config.json` | Global configuration: `EnableAI`, `AIConfigs`, `ServerPaths`, `LockedServers`, `HiddenServers`, `DefaultAIConfig`, `StatusQueryIntervalMs`, `AgentPermissions` |
| `%APPDATA%\MSL_CLI\config.json.bak` | Backup written before every save |
| `%APPDATA%\MSL_CLI\config.json.corrupt` | Quarantined original file when loading fails |
| Log directory under `%APPDATA%\MSL_CLI\` | Runtime log files |

The GUI and the CLI share these paths, so entries registered on either side are visible on the other.

---

## Project Layout

```text
MinecraftServerLauncher/
├─ Models/
│  ├─ Domain/            domain models (AppConfig, AIConfig, ServerProperties, permissions, chat parsing…)
│  ├─ Ports/             interfaces (IServer, IAgentService, IConfigurationStore…)
│  ├─ UseCases/          use cases (CommandExecutor)
│  ├─ Infrastructure/    implementations (ServerManager, AgentService, OpenAi, Query, Commands…)
│  └─ Cli/               console input / output implementations
├─ ViewModels/           MVVM view models (pages + application shell)
├─ Views/                Avalonia views (.axaml)
├─ Assets/               icons and other assets
├─ .shots/               UI screenshots and capture script
├─ App.axaml(.cs)        application entry point and initial-page parsing
└─ Program.cs            Avalonia bootstrap
```

---

## Building from Source

```powershell
# restore and build
dotnet build MinecraftServerLauncher.csproj -c Release

# run directly
dotnet run --project MinecraftServerLauncher.csproj

# publish as a framework-dependent executable
dotnet publish MinecraftServerLauncher.csproj -c Release -r win-x64 --self-contained false
```

Output lands in `bin\Release\net10.0\` (or the corresponding `publish\` folder).

---

## License

Released under the [MIT License](LICENSE), Copyright (c) 2026 LJK0890.
