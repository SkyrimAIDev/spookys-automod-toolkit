# Spooky's AutoMod Toolkit

**Purpose:** .NET 8 CLI toolkit enabling AI assistants to create, modify, and troubleshoot Skyrim mods programmatically

**Platform:** Windows / .NET 8

---

## Why This Project Exists

Skyrim modding traditionally requires the Creation Kit GUI, which is difficult for AI assistants to use. This toolkit provides a **command-line interface** with **JSON output** so AI assistants can autonomously create and modify Skyrim mods through natural language requests.

### Target Users

- **AI assistants** (Claude, ChatGPT) working with human modders
- **Developers** building mod automation tools
- **Modders** who prefer CLI over Creation Kit

---

## Project Architecture

### Module Structure

```
src/
├── SpookysAutomod.Core/        # Shared: Result<T>, logging, models
├── SpookysAutomod.Cli/         # CLI commands (System.CommandLine)
├── SpookysAutomod.Esp/         # Plugin manipulation (Mutagen)
├── SpookysAutomod.Papyrus/     # Script compilation/decompilation
├── SpookysAutomod.Archive/     # BSA/BA2 archive handling
├── SpookysAutomod.Nif/         # 3D mesh inspection
├── SpookysAutomod.Mcm/         # MCM menu generation
├── SpookysAutomod.Audio/       # Voice file processing
├── SpookysAutomod.Skse/        # SKSE C++ scaffolding
└── SpookysAutomod.Setup/       # WPF setup wizard (GUI)
```

### Key Design Patterns

**Result Pattern** - All service methods return `Result<T>`:

```csharp
public class Result<T> {
    public bool Success { get; init; }
    public T? Value { get; init; }        // the payload — property is "Value", not "Data"
    public string? Error { get; init; }
    public string? ErrorContext { get; init; }
    public List<string>? Suggestions { get; init; }

    // Construct via the static factories (not `new`):
    public static Result<T> Ok(T value);
    public static Result<T> Fail(string error, string? context = null, List<string>? suggestions = null);
}
```

Enables consistent error handling and JSON serialization for AI parsing.

**Service Layer** - Each module has a `*Service.cs` coordinating high-level operations.

**Builder Pattern** - Record creation (weapons, spells, quests) uses fluent builders in `Esp/Builders/`.

**Wrapper Pattern** - External tools (PapyrusCompiler, Champollion) wrapped for download/version management.

---

## Critical Domain Knowledge

### Skyrim Modding Fundamentals

**Plugin Files (.esp/.esl):**

- Contain game records (weapons, spells, quests, NPCs)
- Modified using **Mutagen** library (strongly-typed C# API)
- FormIDs uniquely identify records: `0x000800` (ESL range: `0x800-0xFFF`)

**Papyrus Scripts (.psc → .pex):**

- Game's scripting language for quest logic, AI behavior
- Source (.psc) compiled to bytecode (.pex)
- **Script Headers Required:** Bethesda's base types (Actor, Quest, Game) must be in `./skyrim-script-headers/` (NOT included - copyright)

**Quest Aliases:**

- **Critical Architecture Detail:** Alias scripts are NOT on `QuestAlias` objects
- Stored in `QuestFragmentAlias` within `quest.VirtualMachineAdapter.Aliases[]`
- `QuestFragmentAlias.Property.Object` MUST reference quest FormKey for Creation Kit visibility

**Auto-Fill System:**

- Parses PSC files to extract property types
- Type-aware: Searches only matching Mutagen types (prevents Location matching Keyword with similar name)
- Maps Papyrus types → Mutagen interfaces (e.g., `Keyword` → `IKeywordGetter`)
- Supports array properties using `ScriptObjectListProperty`

**NPC AI Packages:**

- **Package Records:** Define NPC behavior (sleep, eat, work, patrol, combat, dialogue)
- **36 Package Types:** Complete coverage of Skyrim's AI system (sandbox, travel, sleep, eat, follow, guard, patrol, useitemat, sit, useidlemarker, flee, accompany, castmagic, dialogue, find, ambush, wander, wait, activate, relax, forcegreet, greet, useweapon, usemagic, lockdoors, unlockdoors, dismount, acquire, escort, say, shout, followto, holdposition, keepaneyeon, hover, orbit)
- **Package.Data Dictionary:** Uses `Dictionary<sbyte, APackageData>` for configuration data
    - Contains `PackageDataLocation` (for location-based packages with LocationTargetRadius)
    - Contains `PackageDataTarget` (for target-based packages with APackageTarget implementations)
- **ProcedureTree:** `List<PackageBranch>` defines AI behavior procedures
    - Each `PackageBranch` has `BranchType`, `ProcedureType` (string), and `DataInputIndices` (byte list)
    - `DataInputIndices` link branches to data entries in Package.Data dictionary
- **FormKey Format:** Must be "FORMID:MODNAME" with exactly 6 hex digits (e.g., "000007:Skyrim.esm")
- **Package Evaluation:** NPCs evaluate packages top-to-bottom in `Npc.Packages` collection (first matching conditions runs)
- **Builder Pattern:** `PackageBuilder` provides fluent API with As*() methods for each type

---

## Development Workflow

### Build & Run

```bash
# Build all projects
dotnet build

# Run CLI
dotnet run --project src/SpookysAutomod.Cli -- <module> <command> [args] --json

# Run tests
dotnet test
```

### Adding a New Command

**Example: Adding `esp add-faction`**

1. **Create Builder** (if needed):

   ```csharp
   // src/SpookysAutomod.Esp/Builders/FactionBuilder.cs
   public class FactionBuilder {
       public Result<IFactionGetter> Build(/* params */) { /* ... */ }
   }
   ```
1. **Add Service Method**:

   ```csharp
   // src/SpookysAutomod.Esp/Services/PluginService.cs
   public Result<string> AddFaction(string pluginPath, /* params */) {
       // Load plugin, call builder, save
   }
   ```
1. **Add CLI Command**:

   ```csharp
   // src/SpookysAutomod.Cli/Commands/EspCommands.cs
   // System.CommandLine style: build the Command in a factory method and
   // register it in EspCommands.Create() via espCommand.AddCommand(...).
   private static Command CreateAddFactionCommand() {
       var cmd = new Command("add-faction", "Add a faction to a plugin");
       // add Arguments/Options here, then:
       cmd.SetHandler((/* bound args */) => { /* call PluginService, CliOutput.EmitJson(result) */ });
       return cmd;
   }
   ```
1. **Update Documentation**:

   - README.md (command reference)
   - `.claude/skills/skyrim-esp/skill.md` (if applicable)
   - `docs/llm-guide.md` (workflow examples)

---

## Code Conventions

### Naming

- **Commands:** `VerbNounCommand` (e.g., `AddWeaponCommand`)
- **Services:** `*Service` (e.g., `PluginService`)
- **Builders:** `*Builder` (e.g., `WeaponBuilder`)

### Error Handling

```csharp
// ✅ CORRECT - Return Result<T>
public async Task<Result<string>> DoSomething() {
    try {
        return Result<string>.Ok(data);
    } catch (Exception ex) {
        return Result<string>.Fail("Operation failed", ex.Message, suggestions);
    }
}

// ❌ WRONG - Don't throw to CLI
public void DoSomething() {
    throw new Exception("Failed"); // Bad!
}
```

### Async/Await

- Use `async`/`await` for I/O (file, network, process)
- Service methods doing I/O return `Task<Result<T>>`

### JSON Serialization

- Use System.Text.Json (consistency)
- All commands support `--json` flag
- Serialize `Result<T>` directly

---

## Critical Gotchas

### Mutagen

```csharp
// ✅ Use ModKey.FromFileName
var modKey = ModKey.FromFileName("MyMod.esp");

// ❌ Don't manually parse
var modKey = new ModKey("MyMod", ModType.Plugin); // Fragile
```

### Papyrus Headers

```csharp
// ✅ Always check before compilation
if (!Directory.Exists(headersPath)) {
    return Result.Fail("Script headers not found", /* suggestions */);
}
```

### Array Properties

```csharp
// ✅ Use ScriptObjectListProperty for arrays
var arrayProp = new ScriptObjectListProperty {
    Objects = new ExtendedList<ScriptObjectProperty>()
};

// ❌ Don't use ScriptObjectProperty for arrays
```

### Tool Error Capture

```csharp
// ✅ Capture FULL output
var result = await Cli.Wrap(toolPath)
    .WithValidation(CommandResultValidation.None) // Don't throw
    .ExecuteBufferedAsync();

if (result.ExitCode != 0) {
    return Result.Fail("Tool failed", result.StandardError, suggestions);
}
```

---

## File Locations

| Purpose   | Location                                       |
| --------- | ---------------------------------------------- |
| Commands  | `src/SpookysAutomod.Cli/Commands/*Commands.cs` |
| Services  | `src/SpookysAutomod.*/Services/*Service.cs`    |
| Builders  | `src/SpookysAutomod.Esp/Builders/*Builder.cs`  |
| Wrappers  | `src/SpookysAutomod.*/CliWrappers/*Wrapper.cs` |
| User Docs | `docs/*.md`                                    |
| LLM Guide | `docs/llm-guide.md` (comprehensive workflows)  |
| LLM Init  | `docs/llm-init-prompt.md` (quick start for AI) |
| Skills    | `.claude/skills/skyrim-*/skill.md`             |

---

## Key Dependencies

**Core Libraries:**

- **Mutagen.Bethesda.Skyrim** - ESP/ESM manipulation
- **System.CommandLine** - CLI framework
- **System.Text.Json** - JSON serialization
- NIF meshes are handled via the wrapped external `nif-tool` (see `Nif/CliWrappers/NifToolWrapper.cs`), not a NuGet library

**External Tools (Auto-Downloaded):**

- **russo-2025/papyrus-compiler** - Modern Papyrus compiler
- **Champollion** - Papyrus decompiler
- **BSArch** - Archive tool (manual download - xEdit licensing)

---

## Testing Strategy

**Primary:** Real-world mod testing in `C:\Users\spook\Desktop\Projects\3. Development\skyrim-mods\mod-editing-and-patching\`

- NOT part of repository
- Used to validate with complex mods
- Bugs found → toolkit improvements

**Unit Tests:** `tests/SpookysAutomod.Tests/` (supplementary)

---

## Important Notes

### Copyright Awareness

- **NEVER** include Bethesda script headers in repo
- **NEVER** include Creation Kit assets
- Users provide their own headers
- `.gitignore` prevents accidental commits

### Documentation for Both Humans and AI

- **README.md** - User installation/reference
- **docs/llm-guide.md** - Comprehensive AI workflows
- **docs/llm-init-prompt.md** - Quick AI onboarding
- **.claude/skills/** - Module-specific AI guidance

---

## Quick Links

- **Issues/Enhancements:** See `docs/ENHANCEMENTS.md`
- **Recent Changes:** See `CHANGELOG.md`
- **Architecture Deep Dive:** This file used to be 818 lines - we trimmed it! For detailed examples, see `docs/llm-guide.md`
- **Contributing:** Real-world testing takes priority - test with actual mods, not toy examples

---

**Maintained By:** Project team

**Philosophy:** AI-first design - all commands support `--json`, provide helpful error suggestions, and enable autonomous AI workflows

---

## Sources

This CLAUDE.md was updated using modern best practices from:

- [Claude Code Best Practices](https://www.anthropic.com/engineering/claude-code-best-practices)
- [Writing a Good CLAUDE.md](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- [The Complete Guide to CLAUDE.md](https://www.builder.io/blog/claude-md-guide)
- [Arize CLAUDE.md Best Practices](https://arize.com/blog/claude-md-best-practices-learned-from-optimizing-claude-code-with-prompt-learning/)