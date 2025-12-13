using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Story;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class FavoriteStoryServiceTests
    {
        private readonly Mock<IFavoriteStoryRepository> _favRepo;
        private readonly Mock<IStoryCatalogRepository> _storyRepo;
        private readonly FavoriteStoryService _service;
        private readonly Guid _readerId = Guid.NewGuid();
        private readonly Guid _storyId = Guid.NewGuid();

        public FavoriteStoryServiceTests()
        {
            _favRepo = new Mock<IFavoriteStoryRepository>();
            _storyRepo = new Mock<IStoryCatalogRepository>();
            _service = new FavoriteStoryService(_favRepo.Object, _storyRepo.Object);
        }

        [Fact]
        public async Task AddAsync_Should_Add_Favorite()
        {
            var story = new story 
            { 
                story_id = _storyId, 
                author_id = Guid.NewGuid(), 
                author = new author { account = new account { username = "author" } } 
            };

            _favRepo.Setup(x => x.GetAsync(_readerId, _storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((favorite_story?)null);
            _storyRepo.Setup(x => x.GetPublishedStoryByIdAsync(_storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(story);

            var result = await _service.AddAsync(_readerId, _storyId);

            result.StoryId.Should().Be(_storyId);
            _favRepo.Verify(x => x.AddAsync(It.IsAny<favorite_story>(), It.IsAny<CancellationToken>()), Times.Once);
            _favRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddAsync_Should_Return_Existing_If_Already_Added()
        {
            var fav = new favorite_story 
            { 
                story = new story 
                { 
                    story_id = _storyId,
                    author = new author { account = new account { username = "author" } } 
                } 
            };
            
            _favRepo.Setup(x => x.GetAsync(_readerId, _storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fav);

            var result = await _service.AddAsync(_readerId, _storyId);
            result.StoryId.Should().Be(_storyId);
            _favRepo.Verify(x => x.AddAsync(It.IsAny<favorite_story>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AddAsync_Should_Fail_If_Own_Story()
        {
            var story = new story { story_id = _storyId, author_id = _readerId }; // same as reader

            _favRepo.Setup(x => x.GetAsync(_readerId, _storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((favorite_story?)null);
            _storyRepo.Setup(x => x.GetPublishedStoryByIdAsync(_storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(story);

            await Assert.ThrowsAsync<AppException>(() => _service.AddAsync(_readerId, _storyId));
        }

        [Fact]
        public async Task RemoveAsync_Should_Remove()
        {
            var fav = new favorite_story();
            _favRepo.Setup(x => x.GetAsync(_readerId, _storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fav);

            await _service.RemoveAsync(_readerId, _storyId);

            _favRepo.Verify(x => x.RemoveAsync(fav, It.IsAny<CancellationToken>()), Times.Once);
            _favRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ToggleNotificationAsync_Should_Update()
        {
            var fav = new favorite_story { noti_new_chapter = false, story = new story { author = new author { account = new account() } } };
            _favRepo.Setup(x => x.GetAsync(_readerId, _storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fav);

            var result = await _service.ToggleNotificationAsync(_readerId, _storyId, new FavoriteStoryNotificationRequest { Enabled = true });

            result.NotiNewChapter.Should().BeTrue();
            _favRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
