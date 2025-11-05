using System;
using System.Collections.Generic;
using System.Linq;
using Contract.DTOs.Respond.Story;
using Repository.Entities;

namespace Service.Helpers
{
    internal static class StoryCatalogMapper
    {
        internal static StoryCatalogListItemResponse ToListItemResponse(story entity, IDictionary<Guid, int> chapterCounts)
        {
            chapterCounts.TryGetValue(entity.story_id, out var chapterCount);

            var tags = entity.story_tags?
                .Where(st => st.tag != null)
                .Select(st => new StoryTagResponse { TagId = st.tag_id, TagName = st.tag!.tag_name })
                .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<StoryTagResponse>();

            return new StoryCatalogListItemResponse
            {
                StoryId = entity.story_id,
                Title = entity.title,
                AuthorId = entity.author_id,
                AuthorUsername = entity.author.account.username,
                CoverUrl = entity.cover_url,
                IsPremium = entity.is_premium,
                TotalChapters = chapterCount,
                PublishedAt = entity.published_at,
                ShortDescription = BuildShortDescription(entity.desc),
                Tags = tags
            };
        }

        private static string? BuildShortDescription(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc))
            {
                return null;
            }

            var trimmed = desc.Trim();
            if (trimmed.Length <= 200)
            {
                return trimmed;
            }

            return trimmed[..200].TrimEnd() + "...";
        }
    }
}

