using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.AIChat;
using Contract.DTOs.Response.AIChat;
using Repository.DataModels;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class AIChatService : IAIChatService
    {
        private const string PremiumPlanCode = "premium_month";
        private const string SystemPrompt = "You are IOSRA's helpful storytelling assistant. Your sole purpose is to provide concise, friendly answers based ONLY on the information available in the provided database content. If you cannot find the answer within the given context, you MUST state that you do not have information in the database about it, without using external knowledge. Keep content PG-13.";
        private const int MaxHistoryMessages = 40;

        private readonly IAIChatRepository _repository;
        private readonly IOpenAiChatService _chatService;
        private readonly ISubscriptionService _subscriptionService;

        public AIChatService(
            IAIChatRepository repository,
            IOpenAiChatService chatService,
            ISubscriptionService subscriptionService)
        {
            _repository = repository;
            _chatService = chatService;
            _subscriptionService = subscriptionService;
        }

        // Gửi tin nhắn cho AI Assistant (Sử dụng luồng RAG - Retrieval-Augmented Generation)
        // 1. Kiểm tra gói Premium (Tính năng độc quyền).
        // 2. Bước 1: Trích xuất Keyword từ câu hỏi của user.
        // 3. Bước 2: Tìm kiếm thông tin liên quan trong DB (Story, Chapter, Author) dựa trên Keyword.
        // 4. Bước 3: Ghép thông tin tìm được (Context) vào Prompt gửi cho OpenAI.
        // 5. Bước 4: Lưu lịch sử trò chuyện vào Redis/DB.
        public async Task<AiChatHistoryResponse> SendAsync(Guid accountId, AiChatSendRequest request, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                throw new AppException("ValidationFailed", "Tin nhắn là bắt buộc.", 400);
            }

            // Đảm bảo chỉ người dùng Premium mới được chat với AI
            await EnsurePremiumSubscriptionAsync(accountId, ct);

            var sanitized = request.Message.Trim();

            // Bước 1: Gọi AI để phân tích câu hỏi và lấy ra các Keyword quan trọng (Ví dụ: "Truyện tiên hiệp", "Tác giả ABC")
            var keywords = await _chatService.ExtractKeywordsAsync(sanitized, ct);

            // Bước 2: Dò trong Database xem có truyện/chương/tác giả nào khớp với Keyword không
            var searchResults = await _repository.SearchContentAsync(keywords, 3, ct);
            
            var contextString = searchResults.Count > 0 
                ? "\n\nRelevant Database Content:\n" + string.Join("\n", searchResults) 
                : string.Empty;

            // Bước 3: Lấy lịch sử chat (History) để AI nắm được ngữ cảnh cuộc trò chuyện
            var history = await _repository.GetHistoryAsync(accountId, ct);
            
            // Ghép System Prompt (Luật lệ) + Context (Dữ liệu DB) + History + Câu hỏi mới
            var promptMessages = BuildPrompt(history, sanitized, contextString);
            
            // Gọi OpenAI API để lấy câu trả lời
            var reply = await _chatService.ChatAsync(promptMessages, ct);

            var now = TimezoneConverter.VietnamNow;
            var stored = new[]
            {
                AiChatStoredMessage.Create("user", sanitized, now),
                AiChatStoredMessage.Create("assistant", reply, now)
            };

            // Lưu lịch sử chat 
            await _repository.AppendAsync(accountId, stored, ct);
            await _repository.TrimAsync(accountId, MaxHistoryMessages, ct);

            return new AiChatHistoryResponse
            {
                Messages = stored.Select(Map).ToArray()
            };
        }

        public async Task<AiChatHistoryResponse> GetHistoryAsync(Guid accountId, CancellationToken ct = default)
        {
            await EnsurePremiumSubscriptionAsync(accountId, ct);
            var history = await _repository.GetHistoryAsync(accountId, ct);
            return BuildResponse(history);
        }

        private static List<AiChatPromptMessage> BuildPrompt(IReadOnlyList<AiChatStoredMessage> history, string newMessage, string contextString)
        {
            var systemMessage = SystemPrompt + (string.IsNullOrEmpty(contextString) 
                ? "" 
                : "\nUse the following database information to answer if relevant. If the user asks about stories, authors, or chapters found here, prioritize this data." + contextString);

            var prompt = new List<AiChatPromptMessage>
            {
                new AiChatPromptMessage("system", systemMessage)
            };

            foreach (var message in history.TakeLast(MaxHistoryMessages))
            {
                prompt.Add(new AiChatPromptMessage(message.Role, message.Content));
            }

            prompt.Add(new AiChatPromptMessage("user", newMessage));
            return prompt;
        }

        private AiChatHistoryResponse BuildResponse(IReadOnlyList<AiChatStoredMessage> history)
        {
            var messages = history
                .Select(Map)
                .OrderBy(m => m.Timestamp)
                .ToArray();

            return new AiChatHistoryResponse
            {
                Messages = messages
            };
        }

        private static AiChatMessageDto Map(AiChatStoredMessage message)
        {
            var local = DateTime.SpecifyKind(message.Timestamp, DateTimeKind.Unspecified);
            var offset = new DateTimeOffset(local, TimezoneConverter.VietnamOffset);
            return new AiChatMessageDto
            {
                Role = message.Role,
                Content = message.Content,
                Timestamp = offset
            };
        }

        private async Task EnsurePremiumSubscriptionAsync(Guid accountId, CancellationToken ct)
        {
            var status = await _subscriptionService.GetStatusAsync(accountId, ct);
            if (!status.HasActiveSubscription || !string.Equals(status.PlanCode, PremiumPlanCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("SubscriptionRequired", "Bạn cần gói premium_month để sử dụng AI Chat.", 403);
            }
        }
    }
}
