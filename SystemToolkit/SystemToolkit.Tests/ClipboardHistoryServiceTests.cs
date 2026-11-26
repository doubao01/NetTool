using FluentAssertions;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class ClipboardHistoryServiceTests
{
    [Fact]
    public void Add_NewContent_AppearsFirst()
    {
        var service = new ClipboardHistoryService();

        service.Add("first");
        service.Add("second");

        service.GetAll().Select(e => e.Content).Should().Equal("second", "first");
    }

    [Fact]
    public void Add_DuplicateContent_MovesToTopWithoutDuplicating()
    {
        var service = new ClipboardHistoryService();
        service.Add("alpha");
        service.Add("beta");

        service.Add("alpha");

        var all = service.GetAll();
        all.Should().HaveCount(2);
        all[0].Content.Should().Be("alpha");
    }

    [Fact]
    public void Add_ComposesUtf8ByteSize()
    {
        var service = new ClipboardHistoryService();

        var entry = service.Add("你好");

        entry.ByteSize.Should().Be(6);
    }

    [Fact]
    public void Add_EmptyContent_Throws()
    {
        var service = new ClipboardHistoryService();

        var action = () => service.Add(string.Empty);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_OversizedContent_Throws()
    {
        var service = new ClipboardHistoryService();

        var action = () => service.Add(new string('x', ClipboardHistoryService.MaxContentBytes + 1));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MaxItems_TrimsOldestUnpinnedEntries()
    {
        var service = new ClipboardHistoryService { MaxItems = 3 };
        service.Add("one");
        service.Add("two");
        service.Add("three");
        service.Add("four");

        var contents = service.GetAll().Select(e => e.Content).ToList();

        contents.Should().HaveCount(3);
        contents.Should().NotContain("one");
    }

    [Fact]
    public void TogglePin_KeepsPinnedEntriesAboveUnpinned()
    {
        var service = new ClipboardHistoryService();
        var older = service.Add("older");
        service.Add("newer");

        service.TogglePin(older.Id);

        service.GetAll()[0].Content.Should().Be("older");
    }

    [Fact]
    public void TogglePin_UnknownId_ReturnsFalse()
    {
        var service = new ClipboardHistoryService();

        service.TogglePin("missing").Should().BeFalse();
    }

    [Fact]
    public void PinnedEntries_SurviveTrim()
    {
        var service = new ClipboardHistoryService { MaxItems = 2 };
        var pinned = service.Add("keep");
        service.TogglePin(pinned.Id);
        service.Add("a");
        service.Add("b");
        service.Add("c");

        service.GetAll().Select(e => e.Content).Should().Contain("keep");
    }

    [Fact]
    public void Search_MatchesCaseInsensitively()
    {
        var service = new ClipboardHistoryService();
        service.Add("Hello World");
        service.Add("unrelated");

        service.Search("hello").Should().ContainSingle().Which.Content.Should().Be("Hello World");
    }

    [Fact]
    public void Search_EmptyKeyword_ReturnsAll()
    {
        var service = new ClipboardHistoryService();
        service.Add("a");
        service.Add("b");

        service.Search("  ").Should().HaveCount(2);
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var service = new ClipboardHistoryService();
        var entry = service.Add("temp");

        service.Remove(entry.Id).Should().BeTrue();
        service.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_UnknownId_ReturnsFalse()
    {
        var service = new ClipboardHistoryService();

        service.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public void ClearUnpinned_KeepsPinnedOnly()
    {
        var service = new ClipboardHistoryService();
        var pinned = service.Add("keep");
        service.TogglePin(pinned.Id);
        service.Add("drop");

        service.ClearUnpinned();

        service.GetAll().Select(e => e.Content).Should().Equal("keep");
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var service = new ClipboardHistoryService();
        service.Add("x");
        service.Add("y");

        service.Clear();

        service.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Preview_CollapsesNewlines()
    {
        var service = new ClipboardHistoryService();

        var entry = service.Add("line1\nline2\r\nline3");

        entry.Preview.Should().Be("line1 line2  line3");
    }

    [Fact]
    public void Preview_TruncatesLongContent()
    {
        var service = new ClipboardHistoryService();

        var entry = service.Add(new string('a', 200));

        entry.Preview.Length.Should().Be(121);
        entry.Preview.Should().EndWith("…");
    }
}