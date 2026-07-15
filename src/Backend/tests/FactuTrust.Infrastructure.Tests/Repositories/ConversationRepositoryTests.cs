using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class ConversationRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly ConversationRepository _repository;

    public ConversationRepositoryTests()
    {
        _databaseName = $"TestDb_Conversation_{Guid.NewGuid():N}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new ConversationRepository(_contextFactory);
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    [Fact]
    public async Task AddAsync_with_user_message_before_save_persists_user_message()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Bonjour", "mistral");
        conversation.AddMessage(MessageRole.User, "Question test");

        await _repository.AddAsync(conversation);

        var loaded = await _repository.GetByIdAsync(conversation.Id);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Messages);
        Assert.Equal(MessageRole.User, loaded.Messages[0].Role);
        Assert.Equal("Question test", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task AddAsync_then_UpdateAsync_with_messages_should_persist_roundtrip()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Bonjour", "mistral");
        await _repository.AddAsync(conversation);

        conversation.AddMessage(MessageRole.User, "Question test");
        conversation.AddMessage(MessageRole.Assistant, "Réponse test");

        await _repository.UpdateAsync(conversation);

        var loaded = await _repository.GetByIdAsync(conversation.Id);
        Assert.NotNull(loaded);
        Assert.Equal(userId, loaded!.UserId);
        Assert.Equal(2, loaded.Messages.Count);
        Assert.Equal(MessageRole.User, loaded.Messages[0].Role);
        Assert.Equal(MessageRole.Assistant, loaded.Messages[1].Role);
    }

    [Fact]
    public async Task GetByIdForChatAsync_returns_only_recent_messages()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Historique", "mistral");
        await _repository.AddAsync(conversation);

        for (var i = 0; i < 15; i++)
        {
            conversation.AddMessage(MessageRole.User, $"Message {i}");
            conversation.AddMessage(MessageRole.Assistant, $"Réponse {i}");
        }
        await _repository.UpdateAsync(conversation);

        var partial = await _repository.GetByIdForChatAsync(conversation.Id, 4);
        Assert.NotNull(partial);
        Assert.Equal(4, partial!.Messages.Count);
        Assert.Equal("Message 13", partial.Messages[0].Content);
        Assert.Equal("Réponse 14", partial.Messages[3].Content);
    }

    public void Dispose()
    {
    }
}
