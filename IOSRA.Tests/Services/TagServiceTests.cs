using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Tag;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Tag;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Implementations;   // TagService
using Xunit;

public class TagServiceTests
{
    private readonly Mock<ITagRepository> _repo;
    private readonly TagService _svc;

    public TagServiceTests()
    {
        _repo = new Mock<ITagRepository>(MockBehavior.Strict);
        _svc = new TagService(_repo.Object);
    }

    private static tag Tag(Guid id, string name) => new tag { tag_id = id, tag_name = name };

    // ========== GET ALL ==========

    // CASE: GetAll – map đúng TagResponse
    [Fact]
    public async Task GetAllAsync_Should_Map_All_Tags()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<tag> { Tag(a, "Romance"), Tag(b, "Action") });

        var res = await _svc.GetAllAsync(CancellationToken.None);

        res.Should().BeEquivalentTo(new[]
        {
            new TagResponse { TagId = a, Name = "Romance" },
            new TagResponse { TagId = b, Name = "Action" },
        });

        _repo.VerifyAll();
    }

    // ========== CREATE ==========

    // CASE: Create – trim + validate + check duplicate + create
    [Fact]
    public async Task CreateAsync_Should_Trim_Validate_CheckDuplicate_Then_Create()
    {
        var id = Guid.NewGuid();
        var req = new TagCreateRequest { Name = "  Romance  " };

        _repo.Setup(r => r.ExistsByNameAsync("Romance", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync("Romance", It.IsAny<CancellationToken>())).ReturnsAsync(Tag(id, "Romance"));

        var res = await _svc.CreateAsync(req, CancellationToken.None);

        res.TagId.Should().Be(id);
        res.Name.Should().Be("Romance");
        _repo.VerifyAll();
    }

    // CASE: Create – name rỗng -> 400
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Is_Empty()
    {
        var req = new TagCreateRequest { Name = "   " };

        var act = () => _svc.CreateAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Tag name must not be empty*");

        _repo.VerifyNoOtherCalls();
    }

    // CASE: Create – name quá dài -> 400
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Exceeds_MaxLength()
    {
        var req = new TagCreateRequest { Name = new string('x', 65) };

        var act = () => _svc.CreateAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*must not exceed 64*");

        _repo.VerifyNoOtherCalls();
    }

    // CASE: Create – duplicate -> 409
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Already_Exists()
    {
        var req = new TagCreateRequest { Name = "Romance" };
        _repo.Setup(r => r.ExistsByNameAsync("Romance", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _svc.CreateAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Tag already exists*");

        _repo.VerifyAll();
    }

    // ========== UPDATE ==========

    // CASE: Update – not found -> 404
    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Tag_NotFound()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
             .ReturnsAsync((tag?)null);

        var act = () => _svc.UpdateAsync(id, new TagUpdateRequest { Name = "X" }, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Tag was not found*");

        _repo.VerifyAll();
    }

    // CASE: Update – tên không đổi -> không gọi UpdateAsync
    [Fact]
    public async Task UpdateAsync_Should_NoOp_When_Name_Unchanged()
    {
        var id = Guid.NewGuid();
        var entity = Tag(id, "Romance");
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var res = await _svc.UpdateAsync(id, new TagUpdateRequest { Name = "Romance" }, CancellationToken.None);

        res.TagId.Should().Be(id);
        res.Name.Should().Be("Romance");
        _repo.Verify(r => r.UpdateAsync(It.IsAny<tag>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.VerifyAll();
    }

    // CASE: Update – tên đổi, chưa trùng -> gọi Update
    [Fact]
    public async Task UpdateAsync_Should_Update_When_Name_Changed_And_Not_Duplicate()
    {
        var id = Guid.NewGuid();
        var entity = Tag(id, "Old");
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repo.Setup(r => r.ExistsByNameAsync("New", id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(It.Is<tag>(t => t.tag_id == id && t.tag_name == "New"), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var res = await _svc.UpdateAsync(id, new TagUpdateRequest { Name = "New" }, CancellationToken.None);

        res.Name.Should().Be("New");
        _repo.VerifyAll();
    }

    // CASE: Update – tên đổi nhưng trùng -> 409
    [Fact]
    public async Task UpdateAsync_Should_Throw_When_New_Name_Already_Exists()
    {
        var id = Guid.NewGuid();
        var entity = Tag(id, "Old");
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repo.Setup(r => r.ExistsByNameAsync("New", id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _svc.UpdateAsync(id, new TagUpdateRequest { Name = "New" }, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Tag already exists*");

        _repo.VerifyAll();
    }

    // ========== DELETE ==========

    // CASE: Delete – not found -> 404
    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Tag_NotFound()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((tag?)null);

        var act = () => _svc.DeleteAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Tag was not found*");

        _repo.VerifyAll();
    }

    // CASE: Delete – đang được dùng -> 409
    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Tag_InUse()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Tag(id, "Romance"));
        _repo.Setup(r => r.HasStoriesAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _svc.DeleteAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*in use*");

        _repo.VerifyAll();
    }

    // CASE: Delete – xoá thành công
    [Fact]
    public async Task DeleteAsync_Should_Delete_When_Not_InUse()
    {
        var id = Guid.NewGuid();
        var entity = Tag(id, "Romance");
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repo.Setup(r => r.HasStoriesAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.DeleteAsync(entity, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _svc.DeleteAsync(id, CancellationToken.None);

        _repo.VerifyAll();
    }

    // ========== OPTIONS (top/resolve/search) ==========

    // CASE: GetTopOptions – pass-through limit & map Value/Label
    [Fact]
    public async Task GetTopOptionsAsync_Should_Return_Options_From_Top()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetTopAsync(10, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<tag> { Tag(id, "Romance") });

        var res = await _svc.GetTopOptionsAsync(10, CancellationToken.None);

        res.Should().ContainSingle(o => o.Value == id && o.Label == "Romance");
        _repo.VerifyAll();
    }

    // CASE: ResolveOptions – ids rỗng -> trả rỗng, KHÔNG gọi repo
    [Fact]
    public async Task ResolveOptionsAsync_Should_Return_Empty_When_Ids_Empty()
    {
        var res = await _svc.ResolveOptionsAsync(new TagResolveRequest { Ids = new List<Guid>() }, CancellationToken.None);

        res.Should().BeEmpty();
        _repo.VerifyNoOtherCalls();
    }

    // CASE: ResolveOptions – map đúng Value/Label
    [Fact]
    public async Task ResolveOptionsAsync_Should_Map_Resolved_Tags()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        _repo.Setup(r => r.ResolveAsync(It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { a, b })), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<tag> { Tag(a, "Action"), Tag(b, "Romance") });

        var res = await _svc.ResolveOptionsAsync(new TagResolveRequest { Ids = new List<Guid> { a, b } }, CancellationToken.None);

        res.Select(x => x.Value).Should().BeEquivalentTo(new[] { a, b });
        res.Select(x => x.Label).Should().BeEquivalentTo(new[] { "Action", "Romance" });
        _repo.VerifyAll();
    }

    // CASE: GetOptions – q blank -> trả rỗng, KHÔNG gọi repo
    [Fact]
    public async Task GetOptionsAsync_Should_Return_Empty_When_Query_Blank()
    {
        var res = await _svc.GetOptionsAsync("   ", 5, CancellationToken.None);

        res.Should().BeEmpty();
        _repo.VerifyNoOtherCalls();
    }

    // CASE: GetOptions – map Value/Label từ SearchAsync
    [Fact]
    public async Task GetOptionsAsync_Should_Map_Options_From_Search()
    {
        _repo.Setup(r => r.SearchAsync("ro", 5, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<tag> { Tag(Guid.NewGuid(), "Romance") });

        var res = await _svc.GetOptionsAsync("ro", 5, CancellationToken.None);

        res.Should().ContainSingle(x => x.Label == "Romance");
        _repo.VerifyAll();
    }

    // ========== PAGED ==========

    // CASE: GetPaged – giữ nguyên tham số phân trang & map (tag,usage) -> TagPagedItem
    [Fact]
    public async Task GetPagedAsync_Should_Map_From_Tuple_And_Preserve_Paging()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        _repo.Setup(r => r.GetPagedAsync("ro", "name", true, 2, 25, It.IsAny<CancellationToken>()))
             .ReturnsAsync((new List<(tag Tag, int Usage)>
             {
                 (Tag(a, "Romance"), 12),
                 (Tag(b, "Romcom"), 3)
             }, 2));

        var res = await _svc.GetPagedAsync("ro", sort: "name", asc: true, page: 2, pageSize: 25, CancellationToken.None);

        res.Page.Should().Be(2);
        res.PageSize.Should().Be(25);
        res.Total.Should().Be(2);
        res.Items.Should().HaveCount(2);
        res.Items[0].Should().BeEquivalentTo(new TagPagedItem { TagId = a, Name = "Romance", Usage = 12 });

        _repo.VerifyAll();
    }
}
