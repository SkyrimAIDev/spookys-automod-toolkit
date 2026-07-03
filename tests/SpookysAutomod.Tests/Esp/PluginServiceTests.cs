using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Esp.Builders;
using SpookysAutomod.Esp.Services;

namespace SpookysAutomod.Tests.Esp;

public class PluginServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginService _service;

    public PluginServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SpookysAutomodTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new PluginService(new SilentLogger());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public void CreatePlugin_WithValidName_CreatesFile()
    {
        var result = _service.CreatePlugin("TestMod.esp", _tempDir);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.True(File.Exists(result.Value));
    }

    [Fact]
    public void CreatePlugin_AsLight_CreatesLightPlugin()
    {
        var result = _service.CreatePlugin("LightMod.esp", _tempDir, isLight: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);

        // Verify it's actually a light plugin by reading it back
        var info = _service.GetPluginInfo(result.Value);
        Assert.True(info.Success);
        Assert.True(info.Value!.IsLight);
    }

    [Fact]
    public void CreatePlugin_WithAuthor_SetsAuthor()
    {
        var result = _service.CreatePlugin("AuthorMod.esp", _tempDir, author: "TestAuthor");

        Assert.True(result.Success);

        var info = _service.GetPluginInfo(result.Value!);
        Assert.True(info.Success);
        Assert.Equal("TestAuthor", info.Value!.Author);
    }

    [Fact]
    public void GetPluginInfo_NonExistentFile_ReturnsError()
    {
        var result = _service.GetPluginInfo(Path.Combine(_tempDir, "NonExistent.esp"));

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPluginInfo_ValidPlugin_ReturnsInfo()
    {
        // Create a plugin first
        var createResult = _service.CreatePlugin("InfoTest.esp", _tempDir);
        Assert.True(createResult.Success);

        // Get info
        var result = _service.GetPluginInfo(createResult.Value!);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("InfoTest.esp", result.Value.FileName);
    }

    [Fact]
    public void LoadPluginForEdit_ValidPlugin_ReturnsModInstance()
    {
        // Create a plugin first
        var createResult = _service.CreatePlugin("EditTest.esp", _tempDir);
        Assert.True(createResult.Success);

        // Load for edit
        var result = _service.LoadPluginForEdit(createResult.Value!);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void SavePlugin_AfterEdit_PersistsChanges()
    {
        // Create a plugin
        var createResult = _service.CreatePlugin("SaveTest.esp", _tempDir);
        Assert.True(createResult.Success);
        var pluginPath = createResult.Value!;

        // Load, modify, and save
        var loadResult = _service.LoadPluginForEdit(pluginPath);
        Assert.True(loadResult.Success);

        var saveResult = _service.SavePlugin(loadResult.Value!, pluginPath);
        Assert.True(saveResult.Success);

        // Verify file still exists and is valid
        var infoResult = _service.GetPluginInfo(pluginPath);
        Assert.True(infoResult.Success);
    }

    [Fact]
    public void GenerateSeqFile_NoStartEnabledQuests_ReturnsNoQuestsMessage()
    {
        // Create a plugin without any start-enabled quests
        var createResult = _service.CreatePlugin("NoSeq.esp", _tempDir);
        Assert.True(createResult.Success);

        // Try to generate SEQ
        var seqResult = _service.GenerateSeqFile(createResult.Value!, _tempDir);

        // Should indicate no start-enabled quests found
        Assert.False(seqResult.Success);
        Assert.Contains("start-enabled", seqResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlugin_IntoNonExistentDirectory_CreatesDirectoryAndFile()
    {
        // Regression: outputPath is the target directory. The previous code created the parent
        // of outputPath, so writing into a not-yet-existing folder failed.
        var newDir = Path.Combine(_tempDir, "new", "nested", "folder");
        Assert.False(Directory.Exists(newDir));

        var result = _service.CreatePlugin("NestedMod.esp", newDir);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.True(File.Exists(result.Value));
        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void GetPluginInfo_DisposesOverlay_SourceFileCanBeDeleted()
    {
        // Regression: the read-only overlay is memory-mapped and IDisposable. If it is not
        // disposed, the source file stays locked. Deleting it here proves the overlay was released.
        var createResult = _service.CreatePlugin("Disposable.esp", _tempDir);
        Assert.True(createResult.Success);
        var path = createResult.Value!;

        var info = _service.GetPluginInfo(path);
        Assert.True(info.Success);

        File.Delete(path); // throws IOException if the overlay still holds the file
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void GenerateSeqFile_WithStartEnabledQuest_WritesRawFormIdsWithoutCountPrefix()
    {
        // Regression: a .seq file is a raw little-endian list of 4-byte quest FormIDs with no
        // count/header prefix. The previous code prepended a uint count, corrupting the file.
        var mod = new SkyrimMod(ModKey.FromFileName("SeqTest.esp"), SkyrimRelease.SkyrimSE);
        var quest = new QuestBuilder(mod, "SeqQuest").StartEnabled().Build();
        var pluginPath = Path.Combine(_tempDir, "SeqTest.esp");
        mod.WriteToBinary(pluginPath);

        var seqResult = _service.GenerateSeqFile(pluginPath, _tempDir);
        Assert.True(seqResult.Success);

        var bytes = File.ReadAllBytes(seqResult.Value!);
        // Exactly one 4-byte FormID — no leading count word.
        Assert.Equal(4, bytes.Length);
        Assert.Equal(quest.FormKey.ID, BitConverter.ToUInt32(bytes, 0));
    }

    [Fact]
    public void CloneRecord_PreservesGlobalSubtypeAndAssignsNewFormKey()
    {
        // Regression: the old reflection copy coerced every cloned Global to Float and lost data.
        var mod = new SkyrimMod(ModKey.FromFileName("CloneTest.esp"), SkyrimRelease.SkyrimSE);
        var source = mod.Globals.AddNewInt();
        source.EditorID = "SourceGlobal";
        source.Data = 42;

        var result = _service.CloneRecord(mod, "SourceGlobal", "ClonedGlobal");
        Assert.True(result.Success, result.Error);

        var cloned = mod.Globals.FirstOrDefault(g => g.EditorID == "ClonedGlobal");
        Assert.NotNull(cloned);
        Assert.IsType<GlobalInt>(cloned);                     // subtype preserved (not Float)
        Assert.Equal(42, ((GlobalInt)cloned!).Data);          // data preserved
        Assert.NotEqual(source.FormKey, cloned.FormKey);      // genuinely a new record
    }

    [Fact]
    public void CloneRecord_PreservesGetOnlySubrecords()
    {
        // Regression: get-only collections (e.g. FormList.Items, spell Effects, keywords) were
        // dropped because the reflection copy skipped non-writable properties.
        var mod = new SkyrimMod(ModKey.FromFileName("CloneTest.esp"), SkyrimRelease.SkyrimSE);
        var target = mod.Globals.AddNewInt("TargetGlobal");
        var list = new FormListBuilder(mod, "SourceList").AddForm(target.FormKey).Build();
        Assert.Single(list.Items);

        var result = _service.CloneRecord(mod, "SourceList", "ClonedList");
        Assert.True(result.Success);

        var cloned = mod.FormLists.FirstOrDefault(f => f.EditorID == "ClonedList");
        Assert.NotNull(cloned);
        Assert.Single(cloned!.Items);                         // sub-record carried over
        Assert.NotEqual(list.FormKey, cloned.FormKey);
    }

    [Fact]
    public void ViewRecord_ByBareHexFormId_FindsRecord()
    {
        // Regression: FindRecordByFormKey only parsed the "ID:ModKey" form and rejected a bare
        // hex FormID like 0x000800, even though --form-id documents exactly that.
        var mod = new SkyrimMod(ModKey.FromFileName("FormIdTest.esp"), SkyrimRelease.SkyrimSE);
        var weapon = new WeaponBuilder(mod, "TestSword").Build();
        var path = Path.Combine(_tempDir, "FormIdTest.esp");
        mod.WriteToBinary(path);

        var id = weapon.FormKey.ID;

        // Bare hex, with 0x prefix.
        var withPrefix = _service.ViewRecord(path, null, "0x" + id.ToString("X6"), null);
        Assert.True(withPrefix.Success);
        Assert.Equal("TestSword", withPrefix.Value!.EditorId);

        // Bare hex, no prefix.
        var noPrefix = _service.ViewRecord(path, null, id.ToString("X6"), null);
        Assert.True(noPrefix.Success);
        Assert.Equal("TestSword", noPrefix.Value!.EditorId);

        // Fully-qualified ID:ModKey form still works.
        var qualified = _service.ViewRecord(path, null, $"{id:X6}:FormIdTest.esp", null);
        Assert.True(qualified.Success);
        Assert.Equal("TestSword", qualified.Value!.EditorId);
    }
}
