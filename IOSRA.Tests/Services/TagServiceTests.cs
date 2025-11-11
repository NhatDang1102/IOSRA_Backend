using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Tag;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Implementations;
using Xunit;

public class TagServiceTests
{
    // Strict để bắt nhầm call
    private readonly Mock<ITagRepository> _tagRepo;
    private readonly TagService _svc;

    public TagServiceTests()
    {
        _tagRepo = new Mock<ITagRepository>(MockBehavior.Strict);
        _svc = new TagService(_tagRepo.Object);
    }

    // Helper: entity mẫu
    private static tag MakeTag(Guid id, string name) => new tag { tag_id = id, tag_name = name };

    // CASE: GetAll – map đúng về TagResponse
    [Fact]
    public async Task GetAllAsync_Should_Return_Mapped_List()
    {
        var t1 = MakeTag(Guid.NewGuid(), "Action");
        var t2 = MakeTag(Guid.NewGuid(), "Romance");

        _tagRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<tag> { t1, t2 });

        var res = await _svc.GetAllAsync(CancellationToken.None);

        res.Should().HaveCount(2);
        res[0].TagId.Should().Be(t1.tag_id);
        res[0].Name.Should().Be("Action");
        res[1].TagId.Should().Be(t2.tag_id);
        res[1].Name.Should().Be("Romance");

        _tagRepo.VerifyAll();
    }

    // CASE: Create – hợp lệ -> trim + unique + tạo mới
    [Fact]
    public async Task CreateAsync_Should_Trim_Validate_Unique_Then_Create()
    {
        var req = new TagCreateRequest { Name = "  new-tag  " };

        _tagRepo.Setup(r => r.ExistsByNameAsync("new-tag", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        _tagRepo.Setup(r => r.CreateAsync("new-tag", It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeTag(Guid.NewGuid(), "new-tag"));

        var res = await _svc.CreateAsync(req, CancellationToken.None);

        res.Name.Should().Be("new-tag");

        _tagRepo.VerifyAll();
    }

    // CASE: Create – trùng tên -> 409
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Duplicate()
    {
        var req = new TagCreateRequest { Name = "dup" };

        _tagRepo.Setup(r => r.ExistsByNameAsync("dup", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => _svc.CreateAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("Tag already exists.");

        _tagRepo.VerifyAll();
    }

    // CASE: Create – name rỗng -> 400
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_Should_Throw_When_Name_Empty(string? name)
    {
        var req = new TagCreateRequest { Name = name };

        var act = () => _svc.CreateAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("Tag name must not be empty.");

        _tagRepo.VerifyNoOtherCalls();
    }

    // CASE: Create – name quá dài > 64 -> 400 (message có số 64)
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_TooLong()
    {
        var longName = new string('x', 65);
        var req = new TagCreateRequest { Name = longName };

        var act = () => _svc.CreateAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*64*");

        _tagRepo.VerifyNoOtherCalls();
    }

    // CASE: Update – tag không tồn tại -> 404
    [Fact]
    public async Task UpdateAsync_Should_Throw_When_NotFound()
    {
        var id = Guid.NewGuid();
        var req = new Contract.DTOs.Request.Tag.TagUpdateRequest { Name = "abc" };

        _tagRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((tag?)null);

        var act = () => _svc.UpdateAsync(id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("Tag was not found.");

        _tagRepo.VerifyAll();
    }

    // CASE: Update – đổi sang tên mới unique -> cập nhật & trả về
    [Fact]
    public async Task UpdateAsync_Should_Update_When_Name_Changed_And_Unique()
    {
        var id = Guid.NewGuid();
        var entity = MakeTag(id, "old");
        var req = new Contract.DTOs.Request.Tag.TagUpdateRequest { Name = " new " };

        _tagRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
        _tagRepo.Setup(r => r.ExistsByNameAsync("new", id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        _tagRepo.Setup(r => r.UpdateAsync(It.Is<tag>(t => t.tag_id == id && t.tag_name == "new"),
                                          It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var res = await _svc.UpdateAsync(id, req, CancellationToken.None);

        res.TagId.Should().Be(id);
        res.Name.Should().Be("new");

        _tagRepo.VerifyAll();
    }

    // CASE: Update – đổi sang tên đã tồn tại -> 409
    [Fact]
    public async Task UpdateAsync_Should_Throw_When_New_Name_Duplicated()
    {
        var id = Guid.NewGuid();
        var entity = MakeTag(id, "old");
        var req = new Contract.DTOs.Request.Tag.TagUpdateRequest { Name = "dup" };

        _tagRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
        _tagRepo.Setup(r => r.ExistsByNameAsync("dup", id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => _svc.UpdateAsync(id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("Tag already exists.");

        _tagRepo.VerifyAll();
    }

    // CASE: Update – không đổi tên -> không gọi ExistsByName/Update, trả entity hiện tại
    [Fact]
    public async Task UpdateAsync_Should_ShortCircuit_When_Name_Not_Changed()
    {
        var id = Guid.NewGuid();
        var entity = MakeTag(id, "same");
        var req = new Contract.DTOs.Request.Tag.TagUpdateRequest { Name = "same" };

        _tagRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);

        var res = await _svc.UpdateAsync(id, req, CancellationToken.None);

        res.Name.Should().Be("same");
        _tagRepo.Verify(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _tagRepo.Verify(r => r.UpdateAsync(It.IsAny<tag>(), It.IsAny<CancellationToken>()), Times.Never);
        _tagRepo.VerifyAll();
    }

    // CASE: Delete – đang được dùng -> 409
    [Fact]
    public async Task DeleteAsync_Should_Throw_When_InUse()
    {
        var id = Guid.NewGuid();
        var entity = MakeTag(id, "t");

        _tagRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
        _tagRepo.Setup(r => r.HasStoriesAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => _svc.DeleteAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("Tag cannot be deleted because it is in use.");

        _tagRepo.VerifyAll();
    }

    // CASE: Delete – hợp lệ -> gọi DeleteAsync
    [Fact]
    public async Task DeleteAsync_Should_Delete_When_Not_InUse()
    {
        var id = Guid.NewGuid();
        var entity = MakeTag(id, "t");

        _tagRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
        _tagRepo.Setup(r => r.HasStoriesAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        _tagRepo.Setup(r => r.DeleteAsync(entity, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        await _svc.DeleteAsync(id, CancellationToken.None);

        _tagRepo.VerifyAll();
    }
}
