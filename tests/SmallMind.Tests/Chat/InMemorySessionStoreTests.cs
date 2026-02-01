using System;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Chat;

namespace SmallMind.Tests.Chat
{
    public class InMemorySessionStoreTests
    {
        [Fact]
        public async Task GetAsync_NonExistentSession_ReturnsNull()
        {
            // Arrange
            var store = new InMemorySessionStore();

            // Act
            var session = await store.GetAsync("non-existent");

            // Assert
            Assert.Null(session);
        }

        [Fact]
        public async Task UpsertAsync_CreatesNewSession()
        {
            // Arrange
            var store = new InMemorySessionStore();
            var session = new ChatSession
            {
                SessionId = "test-session",
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("test-session");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("test-session", retrieved.SessionId);
        }

        [Fact]
        public async Task UpsertAsync_UpdatesExistingSession()
        {
            // Arrange
            var store = new InMemorySessionStore();
            var session = new ChatSession
            {
                SessionId = "test-session",
                CreatedAt = DateTime.UtcNow
            };

            await store.UpsertAsync(session);

            // Modify session
            session.Turns.Add(new ChatTurn
            {
                UserMessage = "Hello",
                AssistantMessage = "Hi there!"
            });

            // Act
            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("test-session");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Single(retrieved.Turns);
        }

        [Fact]
        public async Task DeleteAsync_RemovesSession()
        {
            // Arrange
            var store = new InMemorySessionStore();
            var session = new ChatSession
            {
                SessionId = "test-session",
                CreatedAt = DateTime.UtcNow
            };

            await store.UpsertAsync(session);

            // Act
            await store.DeleteAsync("test-session");
            var retrieved = await store.GetAsync("test-session");

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task ExistsAsync_ExistingSession_ReturnsTrue()
        {
            // Arrange
            var store = new InMemorySessionStore();
            var session = new ChatSession
            {
                SessionId = "test-session",
                CreatedAt = DateTime.UtcNow
            };

            await store.UpsertAsync(session);

            // Act
            var exists = await store.ExistsAsync("test-session");

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_NonExistentSession_ReturnsFalse()
        {
            // Arrange
            var store = new InMemorySessionStore();

            // Act
            var exists = await store.ExistsAsync("non-existent");

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public void Count_ReturnsCorrectSessionCount()
        {
            // Arrange
            var store = new InMemorySessionStore();

            // Act & Assert
            Assert.Equal(0, store.Count);

            store.UpsertAsync(new ChatSession { SessionId = "session1" }).Wait();
            Assert.Equal(1, store.Count);

            store.UpsertAsync(new ChatSession { SessionId = "session2" }).Wait();
            Assert.Equal(2, store.Count);

            store.DeleteAsync("session1").Wait();
            Assert.Equal(1, store.Count);
        }

        [Fact]
        public void Clear_RemovesAllSessions()
        {
            // Arrange
            var store = new InMemorySessionStore();
            store.UpsertAsync(new ChatSession { SessionId = "session1" }).Wait();
            store.UpsertAsync(new ChatSession { SessionId = "session2" }).Wait();

            // Act
            store.Clear();

            // Assert
            Assert.Equal(0, store.Count);
        }

        [Fact]
        public async Task UpsertAsync_UpdatesLastUpdatedAt()
        {
            // Arrange
            var store = new InMemorySessionStore();
            var session = new ChatSession
            {
                SessionId = "test-session",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow.AddHours(-1)
            };

            var oldLastUpdated = session.LastUpdatedAt;

            // Wait a bit to ensure time difference
            await Task.Delay(10);

            // Act
            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("test-session");

            // Assert
            Assert.NotNull(retrieved);
            Assert.True(retrieved.LastUpdatedAt > oldLastUpdated);
        }
    }
}
